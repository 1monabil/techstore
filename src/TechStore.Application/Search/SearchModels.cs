namespace TechStore.Application.Search;

public sealed record SearchQuery
{
    public string? Q { get; init; }
    public string? Category { get; init; }
    public IReadOnlyList<string> Brands { get; init; } = [];
    public decimal? MinPrice { get; init; }
    public decimal? MaxPrice { get; init; }
    public decimal? MinRating { get; init; }
    public bool? InStock { get; init; }
    /// <summary>Spec filters keyed by <see cref="SearchFields.Specs"/> key; values OR'd within a key.</summary>
    public IReadOnlyDictionary<string, IReadOnlyList<string>> Specs { get; init; } =
        new Dictionary<string, IReadOnlyList<string>>();
    public string Sort { get; init; } = SortOptions.Relevance;
    public int Page { get; init; } = 1;
    public int PageSize { get; init; } = 24;
    public string? Cursor { get; init; }
    public bool Explain { get; init; }

    public const int MaxPage = 50;
    public const int MaxPageSize = 60;

    public bool HasText => !string.IsNullOrWhiteSpace(Q);
}

public sealed record SearchResult(
    IReadOnlyList<ProductCard> Items,
    long Total,
    bool TotalIsApproximate,
    int Page,
    int PageSize,
    string? NextCursor,
    IReadOnlyList<Facet> Facets,
    string? DidYouMean,
    long TookMs,
    IReadOnlyList<ScoreExplanation>? Explain = null);

public sealed record ProductCard(
    long Id,
    string Sku,
    string Name,
    string Slug,
    string Brand,
    string Category,
    decimal Price,
    decimal? DiscountPrice,
    double Rating,
    int ReviewCount,
    bool InStock,
    string? ImageUrl,
    IReadOnlyList<string> Specs,
    Highlights? Highlights);

public sealed record Highlights(string? Name, IReadOnlyList<string>? Description);

/// <param name="Min">For the price facet: lowest price among matching products (slider bounds).</param>
/// <param name="Max">For the price facet: highest price among matching products.</param>
public sealed record Facet(string Key, string Label, string Type, IReadOnlyList<FacetBucket> Buckets, decimal? Min = null, decimal? Max = null);

public sealed record FacetBucket(string Value, string Label, long Count, bool Selected);

public sealed record ScoreExplanation(long Id, double? Score, string Explanation);

public sealed record AutocompleteResult(
    IReadOnlyList<SuggestedProduct> Products,
    IReadOnlyList<SuggestedTerm> Brands,
    IReadOnlyList<SuggestedTerm> Categories,
    IReadOnlyList<InstantResult> Instant);

public sealed record SuggestedProduct(string Text, string Slug);

public sealed record SuggestedTerm(string Value, string Label, long Count);

public sealed record InstantResult(long Id, string Name, string Slug, decimal Price, decimal? DiscountPrice, string? ImageUrl);
