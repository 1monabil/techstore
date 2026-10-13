using TechStore.Application.Search;

namespace TechStore.UnitTests;

public class SearchQueryParsingTests
{
    private static (SearchQuery Query, IDictionary<string, string[]> Errors) Parse(params (string Key, string Value)[] pairs) =>
        SearchQueryParser.Parse(pairs.Select(p => new KeyValuePair<string, string?>(p.Key, p.Value)));

    [Fact]
    public void Unknown_keys_are_rejected_not_ignored()
    {
        var (_, errors) = Parse(("q", "rtx"), ("gpuMemory", "16"));

        errors.Should().ContainKey("gpuMemory");
    }

    [Fact]
    public void Lists_and_typed_specs_are_parsed()
    {
        var (q, errors) = Parse(("brands", "asus, MSI,asus"), ("ramGb", "16,32"), ("gpuModel", "GeForce RTX 4070"), ("inStock", "true"));

        errors.Should().BeEmpty();
        q.Brands.Should().BeEquivalentTo("asus", "MSI");
        q.Specs["ramGb"].Should().Equal("16", "32");
        q.Specs["gpuModel"].Should().Equal("GeForce RTX 4070");
        q.InStock.Should().BeTrue();
    }

    [Fact]
    public void Numeric_specs_reject_text()
    {
        var (_, errors) = Parse(("ramGb", "lots"));

        errors.Should().ContainKey("ramGb");
    }

    [Theory]
    [InlineData("page", "51")]
    [InlineData("pageSize", "500")]
    [InlineData("sort", "cheapest")]
    [InlineData("minRating", "6")]
    [InlineData("category", "Laptops & stuff")]
    public void Validator_rejects_out_of_range_values(string key, string value)
    {
        var (q, parseErrors) = Parse((key, value));

        parseErrors.Should().BeEmpty();
        new SearchQueryValidator().Validate(q).IsValid.Should().BeFalse();
    }

    [Fact]
    public void Validator_rejects_inverted_price_range()
    {
        var (q, _) = Parse(("minPrice", "500"), ("maxPrice", "100"));

        new SearchQueryValidator().Validate(q).IsValid.Should().BeFalse();
    }
}
