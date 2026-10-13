using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using TechStore.Domain.Entities;
using TechStore.Infrastructure.Seed;

namespace TechStore.Infrastructure.Persistence;

/// <summary>Applies migrations and the curated seed. Idempotent: safe to run on every startup.</summary>
public sealed class DatabaseInitializer(TechStoreDbContext db, TimeProvider clock, ILogger<DatabaseInitializer> log)
{
    public async Task InitializeAsync(bool seed, CancellationToken ct)
    {
        await RetryAsync(() => db.Database.MigrateAsync(ct), ct);
        if (!seed) return;

        await SeedReferenceDataAsync(ct);
        if (await db.Products.AnyAsync(ct))
        {
            log.LogInformation("Products already present; skipping curated seed");
            return;
        }
        await SeedCuratedProductsAsync(ct);
    }

    private async Task SeedReferenceDataAsync(CancellationToken ct)
    {
        var existingCats = await db.Categories.Select(c => c.Id).ToListAsync(ct);
        foreach (var c in CatalogReference.Categories.Where(c => !existingCats.Contains(c.Id)))
        {
            db.Categories.Add(new Category
            {
                Id = c.Id, Name = c.Name, Slug = c.Slug, ParentId = c.ParentId, SortOrder = c.SortOrder,
                Path = CatalogReference.PathOf(c),
            });
        }

        var existingBrands = await db.Brands.Select(b => b.Id).ToListAsync(ct);
        foreach (var b in CatalogReference.Brands.Where(b => !existingBrands.Contains(b.Id)))
            db.Brands.Add(new Brand { Id = b.Id, Name = b.Name, Slug = b.Slug });

        await db.SaveChangesAsync(ct);
    }

    private async Task SeedCuratedProductsAsync(CancellationToken ct)
    {
        var now = clock.GetUtcNow().UtcDateTime;
        var drafts = CuratedCatalog.Build(now);
        var slugs = new HashSet<string>();

        foreach (var (d, shortText, longText) in drafts)
        {
            var category = CatalogReference.Category(d.CategorySlug);
            var brand = CatalogReference.Brand(d.Brand);
            var product = ToEntity(d, shortText, longText, brand.Id, category.Id);
            // HiLo assigns the id on Add; SKU and slug derive from it, so they are set through the
            // change tracker (the entity's properties are init-only).
            var entry = db.Products.Add(product);
            entry.Property(p => p.Sku).CurrentValue = Slugs.Sku(d.Brand, d.CategorySlug, product.Id);
            var slug = Slugs.From(d.Name);
            entry.Property(p => p.Slug).CurrentValue = slugs.Add(slug) ? slug : $"{slug}-{product.Id}";
            product.Images.Add(new ProductImage { Url = $"/assets/parts/{d.CategorySlug}.svg", IsPrimary = true });
        }

        await db.SaveChangesAsync(ct);
        log.LogInformation("Seeded {Count} curated products", drafts.Count);
    }

    internal static Product ToEntity(ProductDraft d, string shortText, string longText, int brandId, int categoryId) => new()
    {
        Name = d.Name,
        Sku = "",
        Slug = "",
        ShortDescription = shortText,
        Description = longText,
        BrandId = brandId,
        CategoryId = categoryId,
        Price = d.Price,
        DiscountPrice = d.DiscountPrice,
        Currency = "USD",
        StockQuantity = d.StockQuantity,
        Rating = d.Rating,
        ReviewCount = d.ReviewCount,
        SalesCount = d.SalesCount,
        IsActive = true,
        CreatedAt = d.CreatedAt,
        RamGb = d.RamGb,
        StorageGb = d.StorageGb,
        StorageType = d.StorageType,
        CpuBrand = d.CpuBrand,
        CpuModel = d.CpuModel,
        GpuBrand = d.GpuBrand,
        GpuModel = d.GpuModel,
        ScreenSizeInches = d.ScreenSizeInches,
        RefreshRateHz = d.RefreshRateHz,
        Color = d.Color,
        Connectivity = d.Connectivity,
        Attributes = d.Attributes.Select(kv => new ProductAttribute { Key = kv.Key, Value = kv.Value }).ToList(),
    };

    private async Task RetryAsync(Func<Task> action, CancellationToken ct)
    {
        // SQL Server in a fresh container can take a while to accept logins.
        for (var attempt = 1; ; attempt++)
        {
            try { await action(); return; }
            catch (Exception ex) when (attempt < 20 && ex is Microsoft.Data.SqlClient.SqlException or InvalidOperationException)
            {
                log.LogWarning("Database not ready (attempt {Attempt}): {Message}", attempt, ex.Message);
                await Task.Delay(TimeSpan.FromSeconds(3), ct);
            }
        }
    }
}
