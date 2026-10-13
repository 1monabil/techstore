using System.Net;
using System.Net.Http.Json;
using TechStore.Application.Search;

namespace TechStore.IntegrationTests;

[Collection(SearchCollection.Name)]
public class SearchRelevanceTests(SearchFixture fx)
{
    [Fact]
    public async Task Misspelled_brand_still_finds_asus_rog()
    {
        var result = await fx.SearchAsync("q=assus%20rog&pageSize=10");

        result.Items.Should().NotBeEmpty();
        result.Items.Should().OnlyContain(p => p.Brand == "ASUS" && p.Name.Contains("ROG"));
        result.DidYouMean.Should().Be("asus rog");
    }

    [Theory]
    [InlineData("moniter", "Monitors")]
    [InlineData("keybaord", "Keyboards")]
    [InlineData("hedset", "Headsets")]
    public async Task Misspelled_category_words_still_find_the_category(string q, string category)
    {
        var result = await fx.SearchAsync($"q={q}&pageSize=5");

        result.Items.Should().NotBeEmpty().And.OnlyContain(p => p.Category == category);
    }

    [Fact]
    public async Task Glued_and_spaced_model_numbers_match_each_other()
    {
        var glued = await fx.SearchAsync("q=rtx4070&pageSize=60");
        var spaced = await fx.SearchAsync("q=RTX%204070&pageSize=60");

        glued.Items.Should().NotBeEmpty().And.OnlyContain(p => p.Name.Contains("RTX 4070"));
        glued.Items.Select(p => p.Id).Should().BeEquivalentTo(spaced.Items.Select(p => p.Id));
    }

    [Fact]
    public async Task Capacity_written_with_or_without_space_matches()
    {
        var spaced = await fx.SearchAsync("q=990%20pro%202%20tb");

        spaced.Items.Should().Contain(p => p.Name.Contains("990 PRO 2TB"));
    }

    [Theory]
    [InlineData("graphics card")]
    [InlineData("video card")]
    [InlineData("gpu")]
    public async Task Hardware_synonyms_return_graphics_cards(string q)
    {
        var result = await fx.SearchAsync($"q={Uri.EscapeDataString(q)}&pageSize=10");

        result.Items.Should().HaveCount(10).And.OnlyContain(p => p.Category == "Graphics Cards");
    }

    [Fact]
    public async Task Did_you_mean_corrects_typos_across_brand_and_category()
    {
        var result = await fx.SearchAsync("q=lenvo%20labtop");

        result.DidYouMean.Should().StartWith("lenovo lapto");
        result.Items.Should().NotBeEmpty().And.OnlyContain(p => p.Brand == "Lenovo" && p.Category == "Laptops");
    }

    [Fact]
    public async Task Exact_product_name_ranks_first()
    {
        const string name = "Samsung 990 PRO 2TB NVMe M.2 SSD";

        var result = await fx.SearchAsync($"q={Uri.EscapeDataString(name)}");

        result.Items[0].Name.Should().Be(name);
    }

    [Fact]
    public async Task Highlights_wrap_matches_in_mark()
    {
        var result = await fx.SearchAsync("q=ROG%20Strix&pageSize=3");

        result.Items.Should().OnlyContain(p => p.Highlights!.Name!.Contains("<mark>ROG</mark>"));
    }

    [Fact]
    public async Task Parent_category_filter_includes_descendants()
    {
        var parent = await fx.SearchAsync("category=components&pageSize=1");
        var gpus = await fx.SearchAsync("category=graphics-cards&pageSize=1");
        var cpus = await fx.SearchAsync("category=processors&pageSize=1");

        parent.Total.Should().BeGreaterThan(gpus.Total + cpus.Total);
    }

    [Fact]
    public async Task Price_sort_and_cursor_pagination_cover_every_product_once()
    {
        var first = await fx.SearchAsync("category=components&sort=price_asc&pageSize=24");
        var seen = first.Items.Select(i => i.Id).ToList();
        var cursor = first.NextCursor;

        while (cursor is not null)
        {
            var page = await fx.SearchAsync($"category=components&sort=price_asc&pageSize=24&cursor={Uri.EscapeDataString(cursor)}");
            seen.AddRange(page.Items.Select(i => i.Id));
            cursor = page.NextCursor;
        }

        seen.Should().OnlyHaveUniqueItems().And.HaveCount((int)first.Total);
    }

    [Fact]
    public async Task Unknown_filters_are_rejected_with_problem_details()
    {
        var response = await fx.Client.GetAsync("/api/products/search?q=ssd&wattage=850");

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        (await response.Content.ReadAsStringAsync()).Should().Contain("wattage");
    }

    [Fact]
    public async Task Autocomplete_groups_products_brands_and_categories()
    {
        var result = await fx.Client.GetFromJsonAsync<AutocompleteResult>("/api/products/autocomplete?q=corsiar", SearchFixture.Json);

        result!.Brands.Should().Contain(b => b.Value == "corsair");
        result.Products.Should().Contain(p => p.Text.StartsWith("Corsair"));

        var monitors = await fx.Client.GetFromJsonAsync<AutocompleteResult>("/api/products/autocomplete?q=moni", SearchFixture.Json);
        monitors!.Categories.Should().Contain(c => c.Value == "monitors");
        monitors.Instant.Should().NotBeEmpty();
    }
}
