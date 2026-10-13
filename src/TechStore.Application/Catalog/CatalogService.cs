using TechStore.Application.Search;

namespace TechStore.Application.Catalog;

/// <summary>Composes the category tree (SQL structure + ES counts) and the home page.</summary>
public sealed class CatalogService(ICategoryRepository categories, IProductSearchService search)
{
    public async Task<IReadOnlyList<CategoryNode>> GetTreeAsync(CancellationToken ct)
    {
        var flat = await categories.GetAllAsync(ct);
        var counts = await search.CategoryCountsAsync(ct);
        return BuildTree(flat, counts);
    }

    public async Task<HomePage> GetHomeAsync(CancellationToken ct)
    {
        var treeTask = GetTreeAsync(ct);
        var bestTask = search.TopAsync(SortOptions.BestSelling, 12, ct);
        var newTask = search.TopAsync(SortOptions.Newest, 12, ct);
        await Task.WhenAll(treeTask, bestTask, newTask);

        var featured = (await treeTask)
            .SelectMany(root => root.Children)
            .OrderByDescending(c => c.ProductCount)
            .ToList();
        return new HomePage(featured, await bestTask, await newTask);
    }

    /// <summary>Counts are keyed by category slug; a node's count already includes descendants.</summary>
    public static List<CategoryNode> BuildTree(IReadOnlyList<CategoryFlat> flat, IReadOnlyDictionary<string, long> counts)
    {
        var byParent = flat.ToLookup(c => c.ParentId);

        List<CategoryNode> Build(int? parentId) => byParent[parentId]
            .OrderBy(c => c.SortOrder).ThenBy(c => c.Name)
            .Select(c => new CategoryNode(c.Id, c.Name, c.Slug, c.Path,
                counts.TryGetValue(c.Slug, out var n) ? n : 0, Build(c.Id)))
            .ToList();

        return Build(null);
    }
}
