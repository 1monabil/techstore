using TechStore.Application.Search;

namespace TechStore.Application.Catalog;

public sealed record CategoryFlat(int Id, string Name, string Slug, int? ParentId, string Path, int SortOrder);

public sealed record CategoryNode(int Id, string Name, string Slug, string Path, long ProductCount, IReadOnlyList<CategoryNode> Children);

public sealed record ProductDetail(
    long Id,
    string Sku,
    string Name,
    string Slug,
    string ShortDescription,
    string Description,
    string Brand,
    string BrandSlug,
    string Category,
    string CategorySlug,
    decimal Price,
    decimal? DiscountPrice,
    string Currency,
    int StockQuantity,
    decimal Rating,
    int ReviewCount,
    IReadOnlyList<SpecRow> Specs,
    IReadOnlyList<string> Images);

public sealed record SpecRow(string Label, string Value);

public sealed record HomePage(
    IReadOnlyList<CategoryNode> FeaturedCategories,
    IReadOnlyList<ProductCard> BestSellers,
    IReadOnlyList<ProductCard> NewArrivals);
