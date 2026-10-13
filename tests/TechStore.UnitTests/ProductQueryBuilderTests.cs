using System.Text.Json.Nodes;
using TechStore.Application.Search;
using TechStore.Infrastructure.Search;

namespace TechStore.UnitTests;

public class ProductQueryBuilderTests
{
    private static readonly ProductQueryBuilder.Page FirstPage = new(0, 24, null, null, "");

    private static JsonObject Build(SearchQuery q, TextMode mode = TextMode.Exact) =>
        ProductQueryBuilder.Hits(q, mode, FirstPage, trackTotalHits: 10_000);

    private static IReadOnlyList<JsonObject> BuildFacets(SearchQuery q, TextMode mode = TextMode.Exact) => ProductQueryBuilder.Facets(q, mode);

    private static readonly SearchQuery Filtered = new()
    {
        Q = "gaming laptop",
        Category = "laptops",
        Brands = ["asus", "msi"],
        MaxPrice = 2000,
        Specs = new Dictionary<string, IReadOnlyList<string>> { ["ramGb"] = ["32"] },
    };

    [Fact]
    public void Selected_filters_are_non_scoring_filter_clauses_of_the_hits_query()
    {
        var query = Build(Filtered)["query"]!["bool"]!;

        var filter = query["filter"]!.ToJsonString();
        filter.Should().Contain("brandSlug").And.Contain("categoryPath").And.Contain("\"price\"").And.Contain("ramGb");
        query["must"]!.ToJsonString().Should().NotContain("brandSlug");
        query["should"]!.ToJsonString().Should().NotContain("brandSlug");
    }

    [Fact]
    public void Each_facet_applies_every_other_filter_but_not_its_own()
    {
        var main = BuildFacets(Filtered)[0];
        var aggs = main["aggs"]!;

        // The selected category applies to every facet except its own, so it scopes the whole request.
        main["query"]!.ToJsonString().Should().Contain("categoryPath");
        aggs.AsObject().ContainsKey("category").Should().BeFalse();

        var brandFilter = aggs["brands"]!["filter"]!.ToJsonString();
        brandFilter.Should().NotContain("brandSlug");
        brandFilter.Should().Contain("ramGb").And.Contain("\"price\"");

        var ramFilter = aggs["spec_ramGb"]!["filter"]!.ToJsonString();
        ramFilter.Should().Contain("brandSlug").And.NotContain("ramGb");
    }

    [Fact]
    public void Category_facet_is_a_separate_request_with_all_other_filters()
    {
        var facets = BuildFacets(Filtered);

        facets.Should().HaveCount(2);
        var query = facets[1]["query"]!.ToJsonString();
        query.Should().Contain("brandSlug").And.Contain("ramGb").And.Contain("\"price\"").And.NotContain("categoryPath");
        facets[1]["aggs"]!["category"].Should().NotBeNull();
    }

    [Fact]
    public void Without_a_category_all_facets_share_one_request()
    {
        var facets = BuildFacets(Filtered with { Category = null });

        facets.Should().ContainSingle();
        facets[0]["aggs"]!["category"]!["filter"]!.ToJsonString().Should().Contain("brandSlug");
    }

    [Fact]
    public void Selected_values_get_a_dedicated_bucket_so_they_never_drop_out_of_the_top_n()
    {
        var brands = BuildFacets(Filtered)[0]["aggs"]!["brands"]!["aggs"]!;

        brands["sel"]!["filter"]!.ToJsonString().Should().Contain("\"asus\"").And.Contain("\"msi\"");
    }

    [Fact]
    public void Terms_aggregations_are_capped_with_explicit_shard_size()
    {
        var terms = BuildFacets(Filtered)[0]["aggs"]!["brands"]!["aggs"]!["v"]!["terms"]!;

        ((int)terms["size"]!).Should().Be(FacetConfig.TermsSize);
        ((int)terms["shard_size"]!).Should().BeGreaterThan(FacetConfig.TermsSize);
    }

    [Fact]
    public void Facet_request_is_size_zero_and_does_not_score()
    {
        var facets = BuildFacets(Filtered)[0];

        ((int)facets["size"]!).Should().Be(0);
        facets["query"]!["bool"]!.AsObject().ContainsKey("filter").Should().BeTrue();
        facets["query"]!.ToJsonString().Should().NotContain("rank_feature");
        facets.ContainsKey("post_filter").Should().BeFalse();
    }

    [Fact]
    public void Exact_mode_requires_all_terms_and_has_no_fuzziness()
    {
        var query = Build(Filtered with { Brands = [] })["query"]!.ToJsonString();

        query.Should().Contain("\"operator\":\"and\"").And.NotContain("fuzziness");
    }

    [Fact]
    public void Fuzziness_only_targets_text_fields()
    {
        var must = Build(Filtered, TextMode.Fuzzy)["query"]!["bool"]!["must"]![0]!["bool"]!["should"]!.AsArray();

        var mm = must.Select(c => c!["multi_match"]).First(x => x is not null)!;
        ((string)mm["fuzziness"]!).Should().Be("AUTO");
        ((int)mm["prefix_length"]!).Should().Be(1);
        ((int)mm["max_expansions"]!).Should().Be(50);
        mm["fields"]!.AsArray().Select(f => (string)f!).Should()
            .OnlyContain(f => !f.Contains("keyword") && !f.StartsWith("price") && !f.StartsWith("ramGb"));
    }

    [Fact]
    public void Business_signals_use_rank_feature_not_scripts()
    {
        var json = Build(new SearchQuery { Q = "ssd" }).ToJsonString();

        json.Should().Contain("rank_feature").And.Contain("popularity").And.Contain("ratingBoost")
            .And.NotContain("\"script\"").And.NotContain("script_score").And.NotContain("function_score");
    }

    [Fact]
    public void Highlighting_html_encodes_source_and_marks_matches()
    {
        var hl = Build(new SearchQuery { Q = "rtx" })["highlight"]!;

        ((string)hl["encoder"]!).Should().Be("html");
        ((string)hl["pre_tags"]![0]!).Should().Be("<mark>");
        ((bool)hl["require_field_match"]!).Should().BeFalse();
        ((int)hl["fields"]!["description"]!["fragment_size"]!).Should().Be(150);
        ((int)hl["fields"]!["description"]!["number_of_fragments"]!).Should().Be(2);
    }

    [Fact]
    public void Every_sort_ends_with_a_unique_tiebreaker_for_search_after()
    {
        foreach (var sort in SortOptions.All)
        {
            var last = ProductQueryBuilder.Sort(sort, hasText: true).Last()!.AsObject();
            last.ContainsKey("id").Should().BeTrue($"sort '{sort}' must be deterministic");
        }
    }

    [Fact]
    public void Deep_pages_use_search_after_instead_of_from()
    {
        var page = new ProductQueryBuilder.Page(0, 24, new JsonArray(199.99, 42), "pit-id", "120s");
        var body = ProductQueryBuilder.Hits(new SearchQuery(), TextMode.Exact, page, 10_000);

        body.ContainsKey("from").Should().BeFalse();
        body["search_after"]!.AsArray().Should().HaveCount(2);
        ((string)body["pit"]!["id"]!).Should().Be("pit-id");
    }

    [Fact]
    public void Autocomplete_always_sends_a_completion_context()
    {
        var withoutCategory = ProductQueryBuilder.Autocomplete("rog", null, 4);
        var withCategory = ProductQueryBuilder.Autocomplete("rog", "laptops", 4);

        withoutCategory["suggest"]!["products"]!["completion"]!["contexts"]!["category"]![0]!.GetValue<string>().Should().Be(ProductQueryBuilder.AllContext);
        withCategory["suggest"]!["products"]!["completion"]!["contexts"]!["category"]![0]!.GetValue<string>().Should().Be("laptops");
    }
}
