using System.Globalization;
using System.Text.Json;
using TechStore.Application.Search;

namespace TechStore.Infrastructure.Search;

/// <summary>Turns the facet aggregations of one search into API facets (labels, selection state, units).</summary>
internal static class FacetMapper
{
    public static List<Facet> Map(IReadOnlyDictionary<string, JsonElement> aggs, SearchQuery q)
    {
        var facets = new List<Facet>();

        facets.Add(TermsFacet(aggs, "category", "category", "Category",
            key => ReferenceData.SplitFacet(key), slug => q.Category == slug));

        var brands = new HashSet<string>(q.Brands, StringComparer.OrdinalIgnoreCase);
        facets.Add(TermsFacet(aggs, "brands", "brands", "Brand",
            key => ReferenceData.SplitFacet(key), slug => brands.Contains(slug)));

        var price = aggs["price"];
        var stats = price.GetProperty("stats");
        decimal? min = stats.GetProperty("min").ValueKind == JsonValueKind.Number ? Math.Floor(stats.GetProperty("min").GetDecimal()) : null;
        decimal? max = stats.GetProperty("max").ValueKind == JsonValueKind.Number ? Math.Ceiling(stats.GetProperty("max").GetDecimal()) : null;
        var priceBuckets = price.GetProperty("v").GetProperty("buckets").EnumerateArray().Select(b =>
        {
            var key = b.GetProperty("key").GetString()!;
            var def = FacetConfig.PriceBuckets.First(x => x.Key == key);
            return new FacetBucket(key, def.Label, b.GetProperty("doc_count").GetInt64(), q.MinPrice == def.From && q.MaxPrice == def.To);
        }).Where(b => b.Count > 0 || b.Selected).ToList();
        facets.Add(new Facet("price", "Price", "range", priceBuckets, min, max));

        var ratingBuckets = aggs["rating"].GetProperty("v").GetProperty("buckets").EnumerateArray().Select(b =>
        {
            var key = b.GetProperty("key").GetString()!;
            var def = FacetConfig.RatingBuckets.First(x => x.Key == key);
            return new FacetBucket(key, def.Label, b.GetProperty("doc_count").GetInt64(), q.MinRating == def.From);
        }).OrderByDescending(b => decimal.Parse(b.Value, CultureInfo.InvariantCulture)).ToList();
        facets.Add(new Facet("minRating", "Rating", "range", ratingBuckets));

        var inStockCount = aggs["inStock"].GetProperty("v").GetProperty("doc_count").GetInt64();
        facets.Add(new Facet("inStock", "Availability", "terms", [new FacetBucket("true", "In stock", inStockCount, q.InStock == true)]));

        foreach (var spec in FacetConfig.SpecFacetsFor(q.Category, q.Specs.Keys))
        {
            var selected = q.Specs.TryGetValue(spec.Key, out var sel) ? new HashSet<string>(sel, StringComparer.OrdinalIgnoreCase) : [];
            var facet = TermsFacet(aggs, $"spec_{spec.Key}", spec.Key, spec.Label,
                key => (key, FormatSpec(spec, key)), value => selected.Contains(value));
            if (facet.Buckets.Count > 0) facets.Add(facet);
        }
        return facets;
    }

    private static Facet TermsFacet(IReadOnlyDictionary<string, JsonElement> aggs, string aggName, string key, string label,
        Func<string, (string Value, string Label)> split, Func<string, bool> isSelected)
    {
        var agg = aggs[aggName];
        var buckets = new List<FacetBucket>();
        var seen = new HashSet<string>();

        void Read(JsonElement terms)
        {
            foreach (var b in terms.GetProperty("buckets").EnumerateArray())
            {
                var raw = b.TryGetProperty("key_as_string", out var ks) ? ks.GetString()! : KeyString(b.GetProperty("key"));
                var (value, text) = split(raw);
                if (!seen.Add(value)) continue;
                buckets.Add(new FacetBucket(value, text, b.GetProperty("doc_count").GetInt64(), isSelected(value)));
            }
        }

        Read(agg.GetProperty("v"));
        if (agg.TryGetProperty("sel", out var sel)) Read(sel.GetProperty("v"));
        return new Facet(key, label, "terms", buckets);
    }

    private static string KeyString(JsonElement key) => key.ValueKind == JsonValueKind.Number
        ? key.GetDecimal().ToString("0.##", CultureInfo.InvariantCulture)
        : key.GetString()!;

    private static string FormatSpec(SpecField spec, string value) => spec.Key switch
    {
        "ramGb" => $"{value} GB",
        "storageGb" => Units.StorageDisplay(int.Parse(value, CultureInfo.InvariantCulture)),
        "screenSize" => $"{value}\"",
        "refreshRate" => $"{value} Hz",
        _ => value,
    };
}
