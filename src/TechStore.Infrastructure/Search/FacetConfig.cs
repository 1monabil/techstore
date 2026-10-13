using TechStore.Application.Search;

namespace TechStore.Infrastructure.Search;

/// <summary>
/// The single place that decides which facets a category shows. Spec keys must exist in
/// <see cref="SearchFields.Specs"/> (the filter whitelist).
/// </summary>
public static class FacetConfig
{
    public const int TermsSize = 30;
    public static int ShardSize(int size) => (int)(size * 1.5) + 10;

    public sealed record PriceBucket(string Key, string Label, decimal? From, decimal? To);
    public sealed record RatingBucket(string Key, string Label, decimal From);

    public static readonly PriceBucket[] PriceBuckets =
    [
        new("0-100", "Under $100", null, 100),
        new("100-250", "$100 – $250", 100, 250),
        new("250-500", "$250 – $500", 250, 500),
        new("500-1000", "$500 – $1,000", 500, 1000),
        new("1000-2000", "$1,000 – $2,000", 1000, 2000),
        new("2000-", "$2,000 & up", 2000, null),
    ];

    public static readonly RatingBucket[] RatingBuckets =
    [
        new("4.5", "4.5 & up", 4.5m),
        new("4", "4 & up", 4m),
        new("3", "3 & up", 3m),
    ];

    private static readonly Dictionary<string, string[]> SpecFacetsByCategory = new()
    {
        ["laptops"] = ["ramGb", "storageGb", "cpuBrand", "gpuModel", "screenSize", "refreshRate"],
        ["tablets"] = ["storageGb", "screenSize", "connectivity", "color"],
        ["smartphones"] = ["storageGb", "ramGb", "color"],
        ["graphics-cards"] = ["gpuBrand", "gpuModel"],
        ["processors"] = ["cpuBrand"],
        ["motherboards"] = ["cpuBrand", "connectivity"],
        ["ram"] = ["ramGb", "color"],
        ["ssds"] = ["storageGb", "storageType"],
        ["hdds"] = ["storageGb"],
        ["monitors"] = ["screenSize", "refreshRate"],
        ["keyboards"] = ["connectivity", "color"],
        ["mice"] = ["connectivity", "color"],
        ["headsets"] = ["connectivity", "color"],
        ["routers"] = ["connectivity"],
        ["cases"] = ["color"],
        // Parent categories: the specs most of their children share.
        ["computers"] = ["ramGb", "storageGb", "screenSize"],
        ["phones"] = ["storageGb", "color"],
        ["components"] = [],
        ["peripherals"] = ["connectivity", "color"],
    };

    /// <summary>
    /// Spec facets for the request: the selected category's, plus any spec the user already filters
    /// on (so an active filter never disappears from the sidebar).
    /// </summary>
    public static IReadOnlyList<SpecField> SpecFacetsFor(string? categorySlug, IEnumerable<string> activeSpecKeys)
    {
        var keys = new List<string>();
        if (categorySlug is not null && SpecFacetsByCategory.TryGetValue(categorySlug, out var configured))
            keys.AddRange(configured);
        keys.AddRange(activeSpecKeys.Where(k => !keys.Contains(k, StringComparer.OrdinalIgnoreCase)));
        return keys.Select(k => SearchFields.Specs[k]).ToList();
    }
}
