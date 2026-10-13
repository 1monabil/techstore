using TechStore.Application.Search;

namespace TechStore.IntegrationTests;

[Collection(SearchCollection.Name)]
public class FacetTests(SearchFixture fx)
{
    private static Dictionary<string, long> Buckets(SearchResult r, string key) =>
        r.Facets.Single(f => f.Key == key).Buckets.ToDictionary(b => b.Value, b => b.Count);

    [Fact]
    public async Task Selecting_a_brand_keeps_other_brands_with_unchanged_counts()
    {
        var none = await fx.SearchAsync("category=laptops&pageSize=1");
        var asus = await fx.SearchAsync("category=laptops&brands=asus&pageSize=1");

        Buckets(asus, "brands").Should().BeEquivalentTo(Buckets(none, "brands"));
        asus.Facets.Single(f => f.Key == "brands").Buckets.Single(b => b.Value == "asus").Selected.Should().BeTrue();
        asus.Total.Should().Be(Buckets(none, "brands")["asus"]);
    }

    [Fact]
    public async Task Brand_counts_respect_other_active_filters()
    {
        var withRam = await fx.SearchAsync("category=laptops&brands=asus&ramGb=32&pageSize=1");
        var lenovo32 = await fx.SearchAsync("category=laptops&brands=lenovo&ramGb=32&pageSize=1");

        // Lenovo's count next to the selected ASUS = what you'd get by switching to Lenovo.
        Buckets(withRam, "brands")["lenovo"].Should().Be(lenovo32.Total);
    }

    [Fact]
    public async Task Spec_counts_respect_the_selected_brands()
    {
        var both = await fx.SearchAsync("category=laptops&brands=asus,msi&ramGb=16&pageSize=1");
        var asusMsi32 = await fx.SearchAsync("category=laptops&brands=asus,msi&ramGb=32&pageSize=1");

        Buckets(both, "ramGb")["32"].Should().Be(asusMsi32.Total);
    }

    [Fact]
    public async Task Price_buckets_add_up_to_the_hits_of_the_other_filters()
    {
        var msi = await fx.SearchAsync("brands=msi&pageSize=1");
        var msiCheap = await fx.SearchAsync("brands=msi&minPrice=0&maxPrice=100&pageSize=1");

        Buckets(msiCheap, "price").Values.Sum().Should().Be(msi.Total);
    }

    [Fact]
    public async Task Category_specific_spec_facets_come_from_config()
    {
        var laptops = await fx.SearchAsync("category=laptops&pageSize=1");
        var monitors = await fx.SearchAsync("category=monitors&pageSize=1");

        laptops.Facets.Select(f => f.Key).Should().Contain(["ramGb", "gpuModel", "screenSize"]);
        monitors.Facets.Select(f => f.Key).Should().Contain(["screenSize", "refreshRate"]).And.NotContain("ramGb");
    }
}
