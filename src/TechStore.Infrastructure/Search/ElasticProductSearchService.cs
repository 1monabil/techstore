using System.Diagnostics;
using System.Text.Json;
using System.Text.Json.Nodes;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using TechStore.Application;
using TechStore.Application.Search;
using HttpMethod = Elastic.Transport.HttpMethod;

namespace TechStore.Infrastructure.Search;

public sealed class ElasticProductSearchService(
    EsLowLevel es,
    CursorCodec cursors,
    ReferenceData reference,
    IOptions<ElasticsearchOptions> esOptions,
    IOptions<SearchOptions> searchOptions,
    ILogger<ElasticProductSearchService> log) : IProductSearchService
{
    public static readonly ActivitySource Activity = new("TechStore.Search");

    private readonly string _alias = esOptions.Value.Alias;
    private readonly SearchOptions _o = searchOptions.Value;

    // ======================= search =======================

    public async Task<SearchResult> SearchAsync(SearchQuery q, CancellationToken ct)
    {
        using var activity = Activity.StartActivity("search");
        var sw = Stopwatch.StartNew();

        var cursor = q.Cursor is null ? null : cursors.Decode(q.Cursor, q);
        var mode = cursor?.Mode ?? (_o.TypoStrategy == "combined" ? TextMode.Combined : TextMode.Exact);
        var pageNumber = cursor?.Page ?? q.Page;

        string? pitId = cursor?.PitId;
        if (cursor is not null && pitId is null) pitId = await OpenPitAsync(ct); // deep scroll starts: pin a snapshot

        var page = cursor is null
            ? new ProductQueryBuilder.Page((q.Page - 1) * q.PageSize, q.PageSize, null, null, "")
            : new ProductQueryBuilder.Page(0, q.PageSize, cursor.SortValues, pitId, KeepAlive);

        // Round trip 1: hits ∥ facets.
        var (exactHits, exactFacets, exactTook) = await HitsAndFacetsAsync(q, mode, page, ct);
        var outcome = new SearchOutcome(exactHits, exactFacets, mode, null, exactTook);
        if (cursor is null && q.HasText && exactHits.Total < _o.DidYouMeanThreshold)
            outcome = await RecoverFromFewResultsAsync(q, page, outcome, ct);
        var (hits, facets, finalMode, didYouMean, tookMs) = outcome;

        string? next = null;
        if (hits.LastSort is not null && hits.Items.Count == q.PageSize && hits.Total > (long)pageNumber * q.PageSize)
            next = cursors.Encode(new CursorCodec.Cursor(hits.LastSort, hits.PitId ?? pitId, finalMode, pageNumber + 1, CursorCodec.Fingerprint(q)));

        activity?.SetTag("es.took_ms", tookMs);
        activity?.SetTag("search.mode", finalMode.ToString());
        log.LogInformation("search q={Query} mode={Mode} hits={Total} es_took={Took}ms total={Elapsed}ms",
            q.Q, finalMode, hits.Total, tookMs, sw.ElapsedMilliseconds);

        return new SearchResult(hits.Items, hits.Total, hits.TotalIsApproximate, pageNumber, q.PageSize, next,
            facets, didYouMean, tookMs, q.Explain ? hits.Explain : null);
    }

    private sealed record SearchOutcome(ParsedHits Hits, List<Facet> Facets, TextMode Mode, string? DidYouMean, long TookMs);

    /// <summary>
    /// Round trip 2, only when the first page found (almost) nothing: did-you-mean, plus the relaxed
    /// typo-tolerant pass when the exact one found fewer than the fallback threshold ("fallback" strategy).
    /// </summary>
    private async Task<SearchOutcome> RecoverFromFewResultsAsync(
        SearchQuery q, ProductQueryBuilder.Page page, SearchOutcome exact, CancellationToken ct)
    {
        var fuzzy = exact.Mode == TextMode.Exact && exact.Hits.Total < _o.FuzzyFallbackThreshold;
        var requests = new List<(JsonObject Body, bool Pit)> { (ProductQueryBuilder.Suggest(q.Q!), false) };
        if (fuzzy)
        {
            requests.Add((ProductQueryBuilder.Hits(q, TextMode.Fuzzy, page, _o.TrackTotalHitsUpTo), false));
            requests.AddRange(ProductQueryBuilder.Facets(q, TextMode.Fuzzy).Select(b => (b, false)));
        }

        using var doc = await MultiSearchAsync(requests, ct);
        var responses = doc.RootElement.GetProperty("responses");
        var outcome = exact with
        {
            DidYouMean = ParseSuggestion(EnsureSucceeded(responses[0]), q.Q!),
            TookMs = exact.TookMs + doc.RootElement.GetProperty("took").GetInt64(),
        };
        if (!fuzzy) return outcome;

        var fuzzyHits = ParseHits(EnsureSucceeded(responses[1]));
        return fuzzyHits.Total > exact.Hits.Total
            ? outcome with { Hits = fuzzyHits, Facets = FacetMapper.Map(CollectAggregations(responses, 2), q), Mode = TextMode.Fuzzy }
            : outcome;
    }

    private string KeepAlive => $"{(int)_o.PitKeepAlive.TotalSeconds}s";

    private sealed record ParsedHits(
        List<ProductCard> Items, long Total, bool TotalIsApproximate, JsonArray? LastSort, string? PitId, List<ScoreExplanation> Explain);

    private async Task<(ParsedHits Hits, List<Facet> Facets, long TookMs)> HitsAndFacetsAsync(
        SearchQuery q, TextMode mode, ProductQueryBuilder.Page page, CancellationToken ct)
    {
        var hitsBody = ProductQueryBuilder.Hits(q, mode, page, _o.TrackTotalHitsUpTo);
        var facetBodies = ProductQueryBuilder.Facets(q, mode);
        var usesPit = page.PitId is not null;
        if (log.IsEnabled(LogLevel.Trace)) log.LogTrace("ES hits {Hits} facets {Facets}", hitsBody.ToJsonString(), string.Join(" | ", facetBodies.Select(b => b.ToJsonString())));

        JsonDocument doc;
        try
        {
            doc = await MultiSearchAsync([(hitsBody, usesPit), .. facetBodies.Select(b => (b, false))], ct);
            if (usesPit && IsPitMissing(doc.RootElement.GetProperty("responses")[0]))
            {
                doc.Dispose();
                throw new EsException("pit expired", 404);
            }
        }
        catch (EsException ex) when (usesPit && ex.Status == 404)
        {
            // Point-in-time expired (client idle > keep-alive): reopen and continue from the same sort values.
            hitsBody["pit"] = new JsonObject { ["id"] = await OpenPitAsync(ct), ["keep_alive"] = KeepAlive };
            doc = await MultiSearchAsync([(hitsBody, true), .. facetBodies.Select(b => (b, false))], ct);
        }

        using (doc)
        {
            var responses = doc.RootElement.GetProperty("responses");
            var hits = ParseHits(EnsureSucceeded(responses[0]));
            var facets = FacetMapper.Map(CollectAggregations(responses, 1), q);
            return (hits, facets, doc.RootElement.GetProperty("took").GetInt64());
        }
    }

    /// <summary>One _msearch round trip; ES runs the searches concurrently.</summary>
    private async Task<JsonDocument> MultiSearchAsync(IReadOnlyList<(JsonObject Body, bool Pit)> searches, CancellationToken ct)
    {
        var sb = new System.Text.StringBuilder();
        foreach (var (body, pit) in searches)
        {
            // PIT searches must not name an index; size-0 facet/suggest searches use the shard request cache.
            var header = new JsonObject();
            if (!pit) header["index"] = _alias;
            if ((int?)body["size"] == 0) header["request_cache"] = true;
            sb.Append(header.ToJsonString()).Append('\n').Append(body.ToJsonString()).Append('\n');
        }
        return await es.SendAsync(HttpMethod.POST, "/_msearch", sb.ToString(), ct);
    }

    private static JsonElement EnsureSucceeded(JsonElement response)
    {
        if (response.TryGetProperty("error", out var error))
        {
            var status = response.TryGetProperty("status", out var s) ? s.GetInt32() : (int?)null;
            throw new EsException($"ES search failed ({status}): {error.GetRawText()}", status);
        }
        return response;
    }

    /// <summary>Merges the aggregations of the facet responses (from <paramref name="first"/> on) by name.</summary>
    private static Dictionary<string, JsonElement> CollectAggregations(JsonElement responses, int first)
    {
        var aggs = new Dictionary<string, JsonElement>();
        for (var i = first; i < responses.GetArrayLength(); i++)
            foreach (var agg in EnsureSucceeded(responses[i]).GetProperty("aggregations").EnumerateObject())
                aggs[agg.Name] = agg.Value;
        return aggs;
    }

    private static bool IsPitMissing(JsonElement response) =>
        response.TryGetProperty("status", out var s) && s.GetInt32() == 404;

    private async Task<string> OpenPitAsync(CancellationToken ct)
    {
        using var doc = await es.SendAsync(HttpMethod.POST, $"/{_alias}/_pit?keep_alive={KeepAlive}", null, ct);
        return doc.RootElement.GetProperty("id").GetString()!;
    }

    private static ParsedHits ParseHits(JsonElement root)
    {
        var hits = root.GetProperty("hits");
        var totalEl = hits.GetProperty("total");
        var total = totalEl.GetProperty("value").GetInt64();
        var approx = totalEl.GetProperty("relation").GetString() == "gte";

        var items = new List<ProductCard>();
        var explain = new List<ScoreExplanation>();
        JsonArray? lastSort = null;
        foreach (var h in hits.GetProperty("hits").EnumerateArray())
        {
            var card = MapCard(h.GetProperty("_source"), h.TryGetProperty("highlight", out var hl) ? hl : null);
            items.Add(card);
            if (h.TryGetProperty("sort", out var sort)) lastSort = JsonNode.Parse(sort.GetRawText())!.AsArray();
            if (h.TryGetProperty("_explanation", out var ex))
            {
                double? score = h.TryGetProperty("_score", out var s) && s.ValueKind == JsonValueKind.Number ? s.GetDouble() : null;
                explain.Add(new ScoreExplanation(card.Id, score, Summarize(ex, 0)));
            }
        }

        var pit = root.TryGetProperty("pit_id", out var p) ? p.GetString() : null;
        return new ParsedHits(items, total, approx, lastSort, pit, explain);
    }

    private static string? ParseSuggestion(JsonElement root, string query)
    {
        if (!root.TryGetProperty("suggest", out var sug) || !sug.TryGetProperty("dym", out var dym)) return null;
        var options = dym[0].GetProperty("options");
        var suggestion = options.GetArrayLength() > 0 ? options[0].GetProperty("text").GetString() : null;
        return suggestion is not null && IsUsefulCorrection(query, suggestion) ? suggestion : null;
    }

    /// <summary>
    /// Rejects "corrections" that only rewrite numbers ("iphone 16 pro 256" → "… 216"): model numbers
    /// and capacities are deliberate, and the suggester has no idea which digits are typos.
    /// </summary>
    internal static bool IsUsefulCorrection(string query, string suggestion)
    {
        var a = query.Trim().ToLowerInvariant().Split(' ', StringSplitOptions.RemoveEmptyEntries);
        var b = suggestion.Trim().ToLowerInvariant().Split(' ', StringSplitOptions.RemoveEmptyEntries);
        if (a.SequenceEqual(b)) return false;
        if (string.Concat(a) == string.Concat(b)) return false; // only re-spaced ("wifi" → "wi fi")
        if (a.Length != b.Length) return true;
        return a.Zip(b).Where(p => p.First != p.Second).Any(p => !p.First.Any(char.IsDigit) && !p.Second.Any(char.IsDigit));
    }

    internal static ProductCard MapCard(JsonElement src, JsonElement? highlight)
    {
        // Highlights are HTML-encoded by ES (encoder: html). When the description had no match, the card
        // snippet falls back to the short description, encoded the same way so the client treats both alike.
        string? hlName = null;
        List<string>? hlDesc = null;
        if (highlight is { } h)
        {
            hlName = h.TryGetProperty("name", out var n) ? n[0].GetString() : null;
            hlDesc = h.TryGetProperty("description", out var d) ? d.EnumerateArray().Select(x => x.GetString()!).ToList() : null;
        }
        if (hlDesc is null && Str(src, "shortDescription") is { Length: > 0 } sd)
            hlDesc = [System.Net.WebUtility.HtmlEncode(sd)];
        var hl = hlName is null && hlDesc is null ? null : new Highlights(hlName, hlDesc);
        var specLine = Str(src, "specLine");
        return new ProductCard(
            src.GetProperty("id").GetInt64(),
            Str(src, "sku") ?? "",
            Str(src, "name") ?? "",
            Str(src, "slug") ?? "",
            Str(src, "brand") ?? "",
            Str(src, "category") ?? "",
            Dec(src, "listPrice") ?? Dec(src, "price") ?? 0,
            Dec(src, "discountPrice"),
            src.TryGetProperty("rating", out var r) ? Math.Round(r.GetDouble(), 1) : 0,
            src.TryGetProperty("reviewCount", out var rc) ? rc.GetInt32() : 0,
            src.TryGetProperty("inStock", out var st) && st.GetBoolean(),
            Str(src, "imageUrl"),
            string.IsNullOrEmpty(specLine) ? [] : specLine.Split(" · "),
            hl);
    }

    private static string? Str(JsonElement e, string p) => e.TryGetProperty(p, out var v) && v.ValueKind == JsonValueKind.String ? v.GetString() : null;
    private static decimal? Dec(JsonElement e, string p) => e.TryGetProperty(p, out var v) && v.ValueKind == JsonValueKind.Number ? Math.Round(v.GetDecimal(), 2) : null;

    private static string Summarize(JsonElement ex, int depth)
    {
        var line = $"{new string(' ', depth * 2)}{ex.GetProperty("value").GetDouble():0.###} {ex.GetProperty("description").GetString()}";
        if (depth >= 3 || !ex.TryGetProperty("details", out var details)) return line;
        return string.Join('\n', new[] { line }.Concat(details.EnumerateArray().Take(6).Select(d => Summarize(d, depth + 1))));
    }

    // ======================= autocomplete & lists =======================

    public async Task<AutocompleteResult> AutocompleteAsync(AutocompleteQuery query, CancellationToken ct)
    {
        using var activity = Activity.StartActivity("autocomplete");
        var text = query.Q.Trim();
        var body = ProductQueryBuilder.Autocomplete(text, query.Category, instantSize: 4);

        var esTask = es.SendAsync(HttpMethod.POST, $"/{_alias}/_search", body.ToJsonString(), ct);
        var refTask = reference.GetAsync(ct);
        using var doc = await esTask;
        var refs = await refTask;
        var root = doc.RootElement;
        activity?.SetTag("es.took_ms", root.GetProperty("took").GetInt64());

        var products = new List<SuggestedProduct>();
        var seenIds = new HashSet<string>();
        foreach (var opt in root.GetProperty("suggest").GetProperty("products")[0].GetProperty("options").EnumerateArray())
        {
            if (!seenIds.Add(opt.GetProperty("_id").GetString()!)) continue;
            var src = opt.GetProperty("_source");
            products.Add(new SuggestedProduct(Str(src, "name")!, Str(src, "slug")!));
            if (products.Count == 5) break;
        }

        var instant = root.GetProperty("hits").GetProperty("hits").EnumerateArray().Select(h =>
        {
            var s = h.GetProperty("_source");
            return new InstantResult(s.GetProperty("id").GetInt64(), Str(s, "name")!, Str(s, "slug")!,
                Dec(s, "listPrice") ?? Dec(s, "price") ?? 0, Dec(s, "discountPrice"), Str(s, "imageUrl"));
        }).ToList();

        var brands = ReferenceData.Match(refs.Brands, text).Take(4).Select(b => new SuggestedTerm(b.Slug, b.Name, b.Count)).ToList();
        var categories = ReferenceData.Match(refs.Categories, text).Take(4).Select(c => new SuggestedTerm(c.Slug, c.Name, c.Count)).ToList();

        return new AutocompleteResult(products, brands, categories, instant);
    }

    public async Task<IReadOnlyList<ProductCard>> TopAsync(string sort, int size, CancellationToken ct)
    {
        using var doc = await es.SendAsync(HttpMethod.POST, $"/{_alias}/_search?request_cache=true",
            ProductQueryBuilder.Top(sort, size).ToJsonString(), ct);
        return doc.RootElement.GetProperty("hits").GetProperty("hits").EnumerateArray()
            .Select(h => MapCard(h.GetProperty("_source"), null)).ToList();
    }

    public async Task<IReadOnlyDictionary<string, long>> CategoryCountsAsync(CancellationToken ct) =>
        (await reference.GetAsync(ct)).CategoryCounts;
}
