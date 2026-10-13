namespace TechStore.Application.Search;

public enum SpecFieldKind { Keyword, Integer, Decimal }

/// <summary>A filterable typed spec. Key = query-string key, Field = ES field.</summary>
public sealed record SpecField(string Key, string Field, string Label, SpecFieldKind Kind, string? Unit = null);

/// <summary>
/// Single whitelist of filterable spec keys. The validator rejects anything not listed here,
/// and the facet config references these by key.
/// </summary>
public static class SearchFields
{
    public static readonly IReadOnlyDictionary<string, SpecField> Specs = new[]
    {
        new SpecField("ramGb", "ramGb", "Memory", SpecFieldKind.Integer, "GB"),
        new SpecField("storageGb", "storageGb", "Storage", SpecFieldKind.Integer, "GB"),
        new SpecField("storageType", "storageType", "Storage type", SpecFieldKind.Keyword),
        new SpecField("cpuBrand", "cpuBrand", "CPU brand", SpecFieldKind.Keyword),
        new SpecField("cpuModel", "cpuModel.keyword", "Processor", SpecFieldKind.Keyword),
        new SpecField("gpuBrand", "gpuBrand", "GPU brand", SpecFieldKind.Keyword),
        new SpecField("gpuModel", "gpuModel.keyword", "Graphics", SpecFieldKind.Keyword),
        new SpecField("screenSize", "screenSize", "Screen size", SpecFieldKind.Decimal, "\""),
        new SpecField("refreshRate", "refreshRate", "Refresh rate", SpecFieldKind.Integer, "Hz"),
        new SpecField("color", "color", "Color", SpecFieldKind.Keyword),
        new SpecField("connectivity", "connectivity", "Connectivity", SpecFieldKind.Keyword),
    }.ToDictionary(s => s.Key, StringComparer.OrdinalIgnoreCase);

    /// <summary>Non-spec query keys the API accepts.</summary>
    public static readonly IReadOnlySet<string> CoreKeys = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
    {
        "q", "category", "brands", "minPrice", "maxPrice", "minRating", "inStock",
        "sort", "page", "pageSize", "cursor", "explain",
    };

    public static bool IsAllowed(string key) => CoreKeys.Contains(key) || Specs.ContainsKey(key);
}

public static class SortOptions
{
    public const string Relevance = "relevance";
    public const string PriceAsc = "price_asc";
    public const string PriceDesc = "price_desc";
    public const string Rating = "rating";
    public const string Newest = "newest";
    public const string BestSelling = "bestselling";

    public static readonly IReadOnlySet<string> All = new HashSet<string>
    {
        Relevance, PriceAsc, PriceDesc, Rating, Newest, BestSelling,
    };
}
