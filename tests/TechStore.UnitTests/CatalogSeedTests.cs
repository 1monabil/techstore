using TechStore.Infrastructure.Seed;

namespace TechStore.UnitTests;

public class CatalogSeedTests
{
    private static readonly IReadOnlyList<(ProductDraft Draft, string Short, string Long)> Catalog =
        CuratedCatalog.Build(new DateTime(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc));

    [Fact]
    public void Curated_catalog_has_300_to_500_unique_products() =>
        Catalog.Select(c => c.Draft.Name).Distinct().Count().Should().Be(Catalog.Count).And.BeInRange(300, 500);

    [Fact]
    public void Every_leaf_category_is_stocked()
    {
        var leaves = CatalogReference.Categories.Where(c => CatalogReference.Categories.All(x => x.ParentId != c.Id)).Select(c => c.Slug);

        Catalog.Select(c => c.Draft.CategorySlug).Distinct().Should().BeEquivalentTo(leaves);
    }

    [Fact]
    public void Prices_ratings_and_brands_are_plausible()
    {
        foreach (var (d, shortText, longText) in Catalog)
        {
            d.Price.Should().BeInRange(9m, 5000m, d.Name);
            (d.DiscountPrice ?? d.Price).Should().BeLessThanOrEqualTo(d.Price, d.Name);
            d.Rating.Should().BeInRange(0m, 5m);
            CatalogReference.Brands.Should().Contain(b => b.Name == d.Brand, d.Name);
            longText.Should().Contain(d.Name);
        }
    }

    [Fact]
    public void Curated_catalog_is_deterministic()
    {
        var again = CuratedCatalog.Build(new DateTime(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc));

        again.Select(x => (x.Draft.Name, x.Draft.Price)).Should().Equal(Catalog.Select(x => (x.Draft.Name, x.Draft.Price)));
    }
}
