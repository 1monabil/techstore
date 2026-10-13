namespace TechStore.Domain.Entities;

public class Product
{
    public long Id { get; init; }
    public string Sku { get; set; } = null!;
    public string Name { get; init; } = null!;
    public string Slug { get; set; } = null!;
    public string ShortDescription { get; init; } = null!;
    public string Description { get; init; } = null!;

    public int BrandId { get; init; }
    public Brand Brand { get; init; } = null!;
    public int CategoryId { get; init; }
    public Category Category { get; init; } = null!;

    public decimal Price { get; set; }
    public decimal? DiscountPrice { get; init; }
    public string Currency { get; init; } = "USD";
    public int StockQuantity { get; init; }
    public decimal Rating { get; init; }
    public int ReviewCount { get; init; }
    public int SalesCount { get; init; }
    public bool IsActive { get; set; } = true;
    public DateTime CreatedAt { get; init; }
    public DateTime UpdatedAt { get; init; }
    public byte[] RowVersion { get; init; } = null!;

    // Typed specs for the most-filtered attributes. Long-tail specs live in Attributes.
    public int? RamGb { get; init; }
    public int? StorageGb { get; init; }
    public string? StorageType { get; init; }
    public string? CpuBrand { get; init; }
    public string? CpuModel { get; init; }
    public string? GpuBrand { get; init; }
    public string? GpuModel { get; init; }
    public decimal? ScreenSizeInches { get; init; }
    public int? RefreshRateHz { get; init; }
    public string? Color { get; init; }
    public string? Connectivity { get; init; }

    public List<ProductAttribute> Attributes { get; init; } = [];
    public List<ProductImage> Images { get; init; } = [];

    public decimal EffectivePrice => DiscountPrice ?? Price;
    public bool InStock => StockQuantity > 0;
}
