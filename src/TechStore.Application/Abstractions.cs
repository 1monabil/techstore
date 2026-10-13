using TechStore.Application.Catalog;
using TechStore.Application.Search;

namespace TechStore.Application;

public interface IProductSearchService
{
    Task<SearchResult> SearchAsync(SearchQuery query, CancellationToken ct);
    Task<AutocompleteResult> AutocompleteAsync(AutocompleteQuery query, CancellationToken ct);
    Task<IReadOnlyList<ProductCard>> TopAsync(string sort, int size, CancellationToken ct);
    Task<IReadOnlyDictionary<string, long>> CategoryCountsAsync(CancellationToken ct);
}

public interface IProductRepository
{
    Task<ProductDetail?> GetBySlugAsync(string slug, CancellationToken ct);
}

public interface ICategoryRepository
{
    Task<IReadOnlyList<CategoryFlat>> GetAllAsync(CancellationToken ct);
}

public interface IReindexService
{
    Task<ReindexReport> ReindexAsync(CancellationToken ct);
}

public interface IReindexRequestQueue
{
    Task<long> EnqueueAsync(CancellationToken ct);
}

public sealed record ReindexReport(string IndexName, long Documents, TimeSpan Elapsed);
