using System.Globalization;
using Microsoft.EntityFrameworkCore;
using TechStore.Application;
using TechStore.Application.Catalog;
using TechStore.Domain.Entities;

namespace TechStore.Infrastructure.Persistence;

internal sealed class ProductRepository(TechStoreDbContext db) : IProductRepository
{
    public async Task<ProductDetail?> GetBySlugAsync(string slug, CancellationToken ct)
    {
        var p = await db.Products.AsNoTracking()
            .Include(x => x.Brand).Include(x => x.Category)
            .Include(x => x.Attributes).Include(x => x.Images)
            .AsSplitQuery()
            .FirstOrDefaultAsync(x => x.Slug == slug && x.IsActive, ct);
        if (p is null) return null;

        return new ProductDetail(
            p.Id, p.Sku, p.Name, p.Slug, p.ShortDescription, p.Description,
            p.Brand.Name, p.Brand.Slug, p.Category.Name, p.Category.Slug,
            p.Price, p.DiscountPrice, p.Currency, p.StockQuantity, p.Rating, p.ReviewCount,
            SpecRows(p).ToList(),
            p.Images.OrderByDescending(i => i.IsPrimary).ThenBy(i => i.SortOrder).Select(i => i.Url).ToList());
    }

    private static IEnumerable<SpecRow> SpecRows(Product p)
    {
        if (p.CpuModel is not null) yield return new("Processor", p.CpuModel);
        if (p.GpuModel is not null) yield return new("Graphics", p.GpuModel);
        if (p.RamGb is not null) yield return new("Memory", $"{p.RamGb} GB");
        if (p.StorageGb is not null) yield return new("Storage", $"{p.StorageGb} GB {p.StorageType}".Trim());
        if (p.ScreenSizeInches is not null) yield return new("Screen size", $"{p.ScreenSizeInches.Value.ToString("0.##", CultureInfo.InvariantCulture)}\"");
        if (p.RefreshRateHz is not null) yield return new("Refresh rate", $"{p.RefreshRateHz} Hz");
        if (p.Connectivity is not null) yield return new("Connectivity", p.Connectivity);
        if (p.Color is not null) yield return new("Color", p.Color);
        foreach (var a in p.Attributes.OrderBy(a => a.Key)) yield return new(a.Key, a.Value);
    }
}

internal sealed class CategoryRepository(TechStoreDbContext db) : ICategoryRepository
{
    public async Task<IReadOnlyList<CategoryFlat>> GetAllAsync(CancellationToken ct) =>
        await db.Categories.AsNoTracking()
            .Select(c => new CategoryFlat(c.Id, c.Name, c.Slug, c.ParentId, c.Path, c.SortOrder))
            .ToListAsync(ct);
}

internal sealed class ReindexRequestQueue(TechStoreDbContext db, TimeProvider clock) : IReindexRequestQueue
{
    public async Task<long> EnqueueAsync(CancellationToken ct)
    {
        var msg = new OutboxMessage
        {
            Type = OutboxMessageTypes.ReindexRequested,
            AggregateId = 0,
            OccurredAt = clock.GetUtcNow().UtcDateTime,
        };
        db.OutboxMessages.Add(msg);
        await db.SaveChangesAsync(ct);
        return msg.Id;
    }
}
