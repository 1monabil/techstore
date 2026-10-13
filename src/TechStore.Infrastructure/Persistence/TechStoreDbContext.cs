using Microsoft.EntityFrameworkCore;
using TechStore.Domain.Entities;

namespace TechStore.Infrastructure.Persistence;

public class TechStoreDbContext(DbContextOptions<TechStoreDbContext> options) : DbContext(options)
{
    public const string ProductIdSequence = "ProductIds";

    public DbSet<Product> Products => Set<Product>();
    public DbSet<Category> Categories => Set<Category>();
    public DbSet<Brand> Brands => Set<Brand>();
    public DbSet<ProductAttribute> ProductAttributes => Set<ProductAttribute>();
    public DbSet<ProductImage> ProductImages => Set<ProductImage>();
    public DbSet<OutboxMessage> OutboxMessages => Set<OutboxMessage>();

    protected override void OnModelCreating(ModelBuilder b)
    {
        // Product ids come from a sequence so (a) EF knows the id before SaveChanges, which lets the
        // outbox interceptor write the message in the same transaction, and (b) the bulk generator
        // can reserve id ranges with sp_sequence_get_range without colliding with app writes.
        b.HasSequence<long>(ProductIdSequence).StartsAt(1).IncrementsBy(100);

        b.Entity<Product>(e =>
        {
            e.ToTable("Products");
            e.Property(p => p.Id).UseHiLo(ProductIdSequence);
            e.Property(p => p.Sku).HasMaxLength(40).IsUnicode(false);
            e.Property(p => p.Name).HasMaxLength(300);
            e.Property(p => p.Slug).HasMaxLength(320).IsUnicode(false);
            e.Property(p => p.ShortDescription).HasMaxLength(500);
            e.Property(p => p.Description).HasMaxLength(4000);
            e.Property(p => p.Price).HasPrecision(18, 2);
            e.Property(p => p.DiscountPrice).HasPrecision(18, 2);
            e.Property(p => p.Currency).HasMaxLength(3).IsUnicode(false).IsFixedLength();
            e.Property(p => p.Rating).HasPrecision(3, 2);
            e.Property(p => p.RowVersion).IsRowVersion();
            e.Property(p => p.StorageType).HasMaxLength(20);
            e.Property(p => p.CpuBrand).HasMaxLength(30);
            e.Property(p => p.CpuModel).HasMaxLength(80);
            e.Property(p => p.GpuBrand).HasMaxLength(30);
            e.Property(p => p.GpuModel).HasMaxLength(80);
            e.Property(p => p.ScreenSizeInches).HasPrecision(4, 1);
            e.Property(p => p.Color).HasMaxLength(30);
            e.Property(p => p.Connectivity).HasMaxLength(60);
            e.Ignore(p => p.EffectivePrice);
            e.Ignore(p => p.InStock);

            e.HasIndex(p => p.Sku).IsUnique();
            e.HasIndex(p => p.Slug).IsUnique();
            e.HasIndex(p => new { p.CategoryId, p.IsActive });
            e.HasIndex(p => p.BrandId);
            e.HasIndex(p => p.UpdatedAt);

            e.HasOne(p => p.Brand).WithMany().HasForeignKey(p => p.BrandId).OnDelete(DeleteBehavior.Restrict);
            e.HasOne(p => p.Category).WithMany().HasForeignKey(p => p.CategoryId).OnDelete(DeleteBehavior.Restrict);
            e.HasMany(p => p.Attributes).WithOne().HasForeignKey(a => a.ProductId).OnDelete(DeleteBehavior.Cascade);
            e.HasMany(p => p.Images).WithOne().HasForeignKey(i => i.ProductId).OnDelete(DeleteBehavior.Cascade);
        });

        b.Entity<Category>(e =>
        {
            e.ToTable("Categories");
            e.Property(c => c.Id).ValueGeneratedNever();
            e.Property(c => c.Name).HasMaxLength(100);
            e.Property(c => c.Slug).HasMaxLength(100).IsUnicode(false);
            e.Property(c => c.Path).HasMaxLength(400).IsUnicode(false);
            e.HasIndex(c => c.Slug).IsUnique();
            e.HasOne(c => c.Parent).WithMany(c => c.Children).HasForeignKey(c => c.ParentId).OnDelete(DeleteBehavior.Restrict);
        });

        b.Entity<Brand>(e =>
        {
            e.ToTable("Brands");
            e.Property(x => x.Id).ValueGeneratedNever();
            e.Property(x => x.Name).HasMaxLength(60);
            e.Property(x => x.Slug).HasMaxLength(60).IsUnicode(false);
            e.HasIndex(x => x.Slug).IsUnique();
        });

        b.Entity<ProductAttribute>(e =>
        {
            e.ToTable("ProductAttributes");
            e.Property(a => a.Key).HasMaxLength(60);
            e.Property(a => a.Value).HasMaxLength(200);
            e.HasIndex(a => a.ProductId);
        });

        b.Entity<ProductImage>(e =>
        {
            e.ToTable("ProductImages");
            e.Property(i => i.Url).HasMaxLength(400);
            e.HasIndex(i => i.ProductId);
        });

        b.Entity<OutboxMessage>(e =>
        {
            e.ToTable("OutboxMessages");
            e.Property(m => m.Type).HasMaxLength(40).IsUnicode(false);
            e.Property(m => m.LastError).HasMaxLength(2000);
            // Only pending rows are ever scanned by the indexer; keep that index tiny.
            e.HasIndex(m => m.Id).HasFilter("[ProcessedAt] IS NULL AND [DeadLettered] = 0").HasDatabaseName("IX_Outbox_Pending");
        });
    }
}
