using System.Globalization;
using System.Text.Json.Nodes;
using TechStore.Application.Search;

namespace TechStore.Infrastructure.Search;

public enum TextMode { Exact, Fuzzy, Combined }

/// <summary>Pure functions from a <see cref="SearchQuery"/> to ES request bodies. No I/O.</summary>
public static class ProductQueryBuilder
{
    public static readonly string[] CardSource =
    [
        "id", "sku", "name", "slug", "brand", "category", "price", "listPrice", "discountPrice",
        "rating", "reviewCount", "inStock", "imageUrl", "specLine", "shortDescription",
    ];

    private static readonly string[] TextFields =
        ["name^5", "brand.text^3", "category.text^2", "gpuModel^3", "cpuModel^3", "description^1"];

    public const string AllContext = "_all";

    public sealed record Page(int From, int Size, JsonArray? SearchAfter, string? PitId, string PitKeepAlive);

    /// <summary>A facet-able filter: its key (to exclude it from its own facet) and its clause.</summary>
    public sealed record Filter(string Key, JsonObject Clause);

    /// <summary>
    /// The scored page of hits: relevance query, selected filters, highlighting. Facets are a
    /// separate request (<see cref="Facets"/>) in the same _msearch, so the selected filters sit in
    /// bool.filter here instead of post_filter: post_filter exists only to keep filters away from
    /// aggregations in the same request, and it makes ES score every text match (or, with no text,
    /// the whole catalogue) before discarding most of them. Results are identical; measured p95 on
    /// facet browsing went from 221 ms to 176 ms (README, "Load test results").
    /// </summary>
    public static JsonObject Hits(SearchQuery q, TextMode mode, Page page, int trackTotalHits)
    {
        var filters = Filters(q);
        var query = MainQuery(q, mode);
        if (filters.Count > 0)
            query["bool"]!["filter"] = new JsonArray(filters.Select(f => (JsonNode)f.Clause.DeepClone()).ToArray());

        var body = new JsonObject
        {
            ["size"] = page.Size,
            ["track_total_hits"] = trackTotalHits,
            ["_source"] = new JsonArray(CardSource.Select(f => (JsonNode)f!).ToArray()),
            ["query"] = query,
            ["sort"] = Sort(q.Sort, q.HasText),
        };
        if (page.SearchAfter is null) body["from"] = page.From;
        else body["search_after"] = page.SearchAfter.DeepClone();
        if (page.PitId is not null)
            body["pit"] = new JsonObject { ["id"] = page.PitId, ["keep_alive"] = page.PitKeepAlive };
        if (q.HasText) body["highlight"] = Highlight();
        if (q.Explain) body["explain"] = true;
        return body;
    }

    /// <summary>
    /// Facet counts only: size 0, the text clause in filter context (no scoring), every facet
    /// wrapped in the other active filters. Being size 0 and score-free, these are served from the
    /// shard request cache on repeats and run in parallel with <see cref="Hits"/>.
    /// When a category is selected it applies to every facet except the category facet itself, so
    /// it moves into the top-level query (aggregations then only visit that category's documents)
    /// and the category facet becomes a second, small request without it.
    /// </summary>
    public static IReadOnlyList<JsonObject> Facets(SearchQuery q, TextMode mode)
    {
        var text = MainQuery(q, mode)["bool"]!["must"]![0]!;
        var filters = Filters(q);
        var category = filters.FirstOrDefault(f => f.Key == "category");
        if (category is null) return [FacetBody([text], Aggregations(q, filters))];

        var others = filters.Where(f => f != category).ToList();
        var main = FacetBody([text, category.Clause], Aggregations(q, others, includeCategory: false));
        var categoryOnly = FacetBody([text, .. others.Select(f => f.Clause)], new JsonObject
        {
            ["category"] = new JsonObject
            {
                ["filter"] = new JsonObject { ["match_all"] = new JsonObject() },
                ["aggs"] = TermsAgg("categoryFacet", FacetConfig.TermsSize, byKey: false),
            },
        });
        return [main, categoryOnly];
    }

    private static JsonObject FacetBody(IEnumerable<JsonNode> filter, JsonObject aggs) => new()
    {
        ["size"] = 0,
        ["track_total_hits"] = false,
        ["query"] = new JsonObject { ["bool"] = new JsonObject { ["filter"] = new JsonArray(filter.Select(c => c.DeepClone()).ToArray()) } },
        ["aggs"] = aggs,
    };

    public static JsonObject Suggest(string text) => new()
    {
        ["size"] = 0,
        ["track_total_hits"] = false,
        ["suggest"] = DidYouMean(text),
    };

    // ---------- relevance ----------

    public static JsonObject MainQuery(SearchQuery q, TextMode mode)
    {
        var must = new JsonArray();
        var should = new JsonArray();

        if (q.HasText)
        {
            var text = q.Q!.Trim();
            var clauses = mode switch
            {
                TextMode.Exact => TextClauses(text, fuzzy: false, boost: 1),
                TextMode.Fuzzy => TextClauses(text, fuzzy: true, boost: 1),
                _ => TextClauses(text, fuzzy: false, boost: 3).Concat(TextClauses(text, fuzzy: true, boost: 1)),
            };
            must.Add(new JsonObject
            {
                ["bool"] = new JsonObject
                {
                    ["should"] = new JsonArray(clauses.Select(c => (JsonNode)c).ToArray()),
                    ["minimum_should_match"] = 1,
                },
            });
            should.Add(new JsonObject { ["match_phrase"] = new JsonObject { ["name"] = new JsonObject { ["query"] = text, ["slop"] = 1, ["boost"] = 4 } } });
            should.Add(new JsonObject { ["term"] = new JsonObject { ["name.keyword"] = new JsonObject { ["value"] = text.ToLowerInvariant(), ["boost"] = 10 } } });
        }
        else
        {
            must.Add(new JsonObject { ["match_all"] = new JsonObject() });
        }

        // Business signals as rank_feature (no scripts): popularity, confidence-weighted rating, availability.
        should.Add(new JsonObject { ["rank_feature"] = new JsonObject { ["field"] = "popularity", ["saturation"] = new JsonObject(), ["boost"] = 2 } });
        should.Add(new JsonObject { ["rank_feature"] = new JsonObject { ["field"] = "ratingBoost", ["saturation"] = new JsonObject(), ["boost"] = 1.5 } });
        should.Add(new JsonObject { ["term"] = new JsonObject { ["inStock"] = new JsonObject { ["value"] = true, ["boost"] = 1 } } });

        return new JsonObject { ["bool"] = new JsonObject { ["must"] = must, ["should"] = should } };
    }

    /// <summary>
    /// best_fields multi_match (per spec) plus the same terms against searchText, so a query whose
    /// terms live in different fields ("lenovo laptop": brand + category) still matches.
    /// </summary>
    private static IEnumerable<JsonObject> TextClauses(string text, bool fuzzy, double boost)
    {
        JsonObject BestFields() => new()
        {
            ["query"] = text,
            ["type"] = "best_fields",
            ["fields"] = new JsonArray(TextFields.Select(f => (JsonNode)f!).ToArray()),
            ["boost"] = boost,
        };
        JsonObject CrossField() => new() { ["query"] = text, ["boost"] = boost * 0.5 };

        if (!fuzzy)
        {
            var bestFields = BestFields();
            var crossField = CrossField();
            bestFields["operator"] = "and";
            crossField["operator"] = "and";
            yield return new JsonObject { ["multi_match"] = bestFields };
            yield return new JsonObject { ["match"] = new JsonObject { ["searchText"] = crossField } };
            // Glued model numbers / capacities typed by the user ("rtx4070", "16gb") against spaced text.
            yield return new JsonObject
            {
                ["match"] = new JsonObject
                {
                    ["searchText.joined"] = new JsonObject { ["query"] = text, ["operator"] = "and", ["boost"] = boost * 0.5 },
                },
            };
            yield break;
        }

        // The relaxed pass: typo-tolerant and no longer requiring every term. Query terms are tried
        // both stemmed (the field's search analyzer) and unstemmed, because the stemmer can move a
        // typo either closer to or further from the indexed (stemmed) term:
        //   "assus" → "assu" is 1 edit from the indexed "asu" (asus), unstemmed is 2;
        //   "moniter" → "monit" is 2 edits from the indexed "monitor", unstemmed is 1.
        foreach (var analyzer in new string?[] { null, "autocomplete_analyzer" })
        {
            var bestFields = BestFields();
            var crossField = CrossField();
            foreach (var clause in new[] { bestFields, crossField })
            {
                clause["fuzziness"] = "AUTO";
                clause["prefix_length"] = 1;
                clause["max_expansions"] = 50;
                clause["fuzzy_transpositions"] = true;
                clause["operator"] = "or";
                clause["minimum_should_match"] = "3<75%";
                if (analyzer is not null) clause["analyzer"] = analyzer;
            }
            yield return new JsonObject { ["multi_match"] = bestFields };
            yield return new JsonObject { ["match"] = new JsonObject { ["searchText"] = crossField } };
        }
    }

    // ---------- sorting ----------

    public static JsonArray Sort(string sort, bool hasText)
    {
        JsonObject F(string field, string order) => new() { [field] = new JsonObject { ["order"] = order } };
        var tiebreak = F("id", "asc"); // stable order → deterministic search_after cursors
        return sort switch
        {
            SortOptions.PriceAsc => [F("price", "asc"), tiebreak],
            SortOptions.PriceDesc => [F("price", "desc"), tiebreak],
            SortOptions.Rating => [F("rating", "desc"), F("reviewCount", "desc"), tiebreak],
            SortOptions.Newest => [F("createdAt", "desc"), tiebreak],
            SortOptions.BestSelling => [F("salesCount", "desc"), tiebreak],
            _ => [F("_score", "desc"), F("salesCount", "desc"), tiebreak],
        };
    }

    // ---------- filters ----------

    public static List<Filter> Filters(SearchQuery q)
    {
        var list = new List<Filter>();
        if (q.Category is not null)
            list.Add(new("category", Term("categoryPath", q.Category)));
        if (q.Brands.Count > 0)
            list.Add(new("brands", Terms("brandSlug", q.Brands.Select(b => (JsonNode)b.ToLowerInvariant()!))));
        if (q.MinPrice.HasValue || q.MaxPrice.HasValue)
        {
            var range = new JsonObject();
            if (q.MinPrice is { } min) range["gte"] = min;
            if (q.MaxPrice is { } max) range["lte"] = max;
            list.Add(new("price", new JsonObject { ["range"] = new JsonObject { ["price"] = range } }));
        }
        if (q.MinRating is { } r)
            list.Add(new("rating", new JsonObject { ["range"] = new JsonObject { ["rating"] = new JsonObject { ["gte"] = r } } }));
        if (q.InStock == true)
            list.Add(new("inStock", Term("inStock", true)));

        foreach (var (key, values) in q.Specs)
        {
            var spec = SearchFields.Specs[key];
            var vals = values.Select(v => spec.Kind == SpecFieldKind.Keyword
                ? (JsonNode)v!
                : (JsonNode)decimal.Parse(v, CultureInfo.InvariantCulture)!);
            list.Add(new(spec.Key, Terms(spec.Field, vals)));
        }
        return list;
    }

    // ---------- facets ----------

    /// <summary>
    /// Multi-select facets: hits are narrowed by post_filter, while each facet's aggregation is
    /// wrapped in a filter agg applying every OTHER active filter. Selecting "ASUS" therefore keeps
    /// the other brands visible, with counts that reflect the remaining filters.
    /// </summary>
    public static JsonObject Aggregations(SearchQuery q, IReadOnlyList<Filter> filters, bool includeCategory = true)
    {
        var aggs = new JsonObject();
        void Add(string key, string excludeKey, JsonObject inner)
        {
            var others = filters.Where(f => f.Key != excludeKey).Select(f => (JsonNode)f.Clause.DeepClone()).ToList();
            aggs[key] = new JsonObject
            {
                ["filter"] = others.Count == 0 ? new JsonObject { ["match_all"] = new JsonObject() } : Bool("filter", others),
                ["aggs"] = inner,
            };
        }

        if (includeCategory) Add("category", "category", TermsAgg("categoryFacet", FacetConfig.TermsSize, byKey: false));
        Add("brands", "brands", WithSelected(TermsAgg("brandFacet", FacetConfig.TermsSize, byKey: false), "brandSlug", "brandFacet",
            q.Brands.Select(b => b.ToLowerInvariant()).ToList()));
        Add("price", "price", new JsonObject
        {
            ["v"] = new JsonObject
            {
                ["range"] = new JsonObject
                {
                    ["field"] = "price",
                    ["keyed"] = false,
                    ["ranges"] = new JsonArray(FacetConfig.PriceBuckets.Select(bucket =>
                    {
                        var range = new JsonObject { ["key"] = bucket.Key };
                        if (bucket.From is { } from) range["from"] = from;
                        if (bucket.To is { } to) range["to"] = to;
                        return (JsonNode)range;
                    }).ToArray()),
                },
            },
            ["stats"] = new JsonObject { ["stats"] = new JsonObject { ["field"] = "price" } },
        });
        Add("rating", "rating", new JsonObject
        {
            ["v"] = new JsonObject
            {
                ["range"] = new JsonObject
                {
                    ["field"] = "rating",
                    ["ranges"] = new JsonArray(FacetConfig.RatingBuckets.Select(b => (JsonNode)new JsonObject { ["key"] = b.Key, ["from"] = b.From }).ToArray()),
                },
            },
        });
        Add("inStock", "inStock", new JsonObject { ["v"] = new JsonObject { ["filter"] = Term("inStock", true) } });

        foreach (var spec in FacetConfig.SpecFacetsFor(q.Category, q.Specs.Keys))
        {
            var numeric = spec.Kind != SpecFieldKind.Keyword;
            var selected = q.Specs.TryGetValue(spec.Key, out var sel) ? sel : null;
            Add($"spec_{spec.Key}", spec.Key, WithSelected(TermsAgg(spec.Field, FacetConfig.TermsSize, byKey: numeric), spec.Field, spec.Field, selected, numeric));
        }
        return aggs;
    }

    private static JsonObject TermsAgg(string field, int size, bool byKey)
    {
        var terms = new JsonObject { ["field"] = field, ["size"] = size, ["shard_size"] = FacetConfig.ShardSize(size) };
        if (byKey) terms["order"] = new JsonObject { ["_key"] = "asc" };
        return new JsonObject { ["v"] = new JsonObject { ["terms"] = terms } };
    }

    /// <summary>Selected values outside the top N still get a bucket (count + selected flag).</summary>
    private static JsonObject WithSelected(JsonObject inner, string filterField, string aggField, IReadOnlyList<string>? selected, bool numeric = false)
    {
        if (selected is null || selected.Count == 0) return inner;
        var values = selected.Select(v => numeric ? (JsonNode)decimal.Parse(v, CultureInfo.InvariantCulture)! : (JsonNode)v!);
        inner["sel"] = new JsonObject
        {
            ["filter"] = Terms(filterField, values),
            ["aggs"] = new JsonObject { ["v"] = new JsonObject { ["terms"] = new JsonObject { ["field"] = aggField, ["size"] = selected.Count } } },
        };
        return inner;
    }

    // ---------- highlight & suggest ----------

    public static JsonObject Highlight() => new()
    {
        ["pre_tags"] = new JsonArray("<mark>"),
        ["post_tags"] = new JsonArray("</mark>"),
        // ES HTML-encodes the source text, so <mark> is the only markup that can come back.
        ["encoder"] = "html",
        ["require_field_match"] = false,
        ["fields"] = new JsonObject
        {
            ["name"] = new JsonObject { ["number_of_fragments"] = 0 },
            ["description"] = new JsonObject { ["fragment_size"] = 150, ["number_of_fragments"] = 2 },
        },
    };

    public static JsonObject DidYouMean(string text) => new()
    {
        ["text"] = text,
        ["dym"] = new JsonObject
        {
            ["phrase"] = new JsonObject
            {
                ["field"] = "dym",
                ["size"] = 1,
                ["gram_size"] = 3,
                ["max_errors"] = 2,
                ["confidence"] = 1.0,
                ["direct_generator"] = new JsonArray(new JsonObject
                {
                    ["field"] = "dym",
                    ["suggest_mode"] = "always",
                    ["min_word_length"] = 3,
                }),
            },
        },
    };

    // ---------- other bodies ----------

    public static JsonObject Autocomplete(string text, string? category, int instantSize)
    {
        var filter = new JsonArray();
        if (category is not null) filter.Add(Term("categoryPath", category));

        var completion = new JsonObject
        {
            ["field"] = "suggest",
            ["size"] = 6,
            ["skip_duplicates"] = true,
            ["fuzzy"] = new JsonObject { ["fuzziness"] = "AUTO", ["prefix_length"] = 1 },
        };
        // Context-enabled completion fields require a context on every query (ES 8+), so every
        // document also carries the catch-all context used when no category is selected.
        completion["contexts"] = new JsonObject { ["category"] = new JsonArray(category ?? AllContext) };

        return new JsonObject
        {
            ["size"] = instantSize,
            ["track_total_hits"] = false,
            ["_source"] = new JsonArray("id", "name", "slug", "price", "listPrice", "discountPrice", "imageUrl", "category"),
            ["query"] = new JsonObject
            {
                ["bool"] = new JsonObject
                {
                    ["must"] = new JsonArray(new JsonObject
                    {
                        ["multi_match"] = new JsonObject
                        {
                            ["query"] = text,
                            ["type"] = "bool_prefix",
                            ["operator"] = "and",
                            ["fields"] = new JsonArray("name.suggest", "name.suggest._2gram", "name.suggest._3gram"),
                        },
                    }),
                    ["filter"] = filter,
                    ["should"] = new JsonArray(new JsonObject { ["rank_feature"] = new JsonObject { ["field"] = "popularity", ["saturation"] = new JsonObject(), ["boost"] = 2 } }),
                },
            },
            ["suggest"] = new JsonObject
            {
                ["products"] = new JsonObject { ["prefix"] = text, ["completion"] = completion },
            },
        };
    }

    public static JsonObject Top(string sort, int size) => new()
    {
        ["size"] = size,
        ["track_total_hits"] = false,
        ["_source"] = new JsonArray(CardSource.Select(f => (JsonNode)f!).ToArray()),
        ["query"] = new JsonObject { ["bool"] = new JsonObject { ["filter"] = new JsonArray(Term("inStock", true)) } },
        ["sort"] = Sort(sort, hasText: false),
    };

    public static JsonObject CategoryCounts() => new()
    {
        ["size"] = 0,
        ["track_total_hits"] = false,
        ["aggs"] = new JsonObject
        {
            ["paths"] = new JsonObject { ["terms"] = new JsonObject { ["field"] = "categoryPath", ["size"] = 200 } },
            ["brands"] = new JsonObject { ["terms"] = new JsonObject { ["field"] = "brandFacet", ["size"] = 500 } },
        },
    };

    // ---------- helpers ----------

    private static JsonObject Term(string field, JsonNode value) => new() { ["term"] = new JsonObject { [field] = value } };
    private static JsonObject Terms(string field, IEnumerable<JsonNode> values) => new() { ["terms"] = new JsonObject { [field] = new JsonArray(values.ToArray()) } };
    private static JsonObject Bool(string occur, IEnumerable<JsonNode> clauses) => new() { ["bool"] = new JsonObject { [occur] = new JsonArray(clauses.ToArray()) } };
}
