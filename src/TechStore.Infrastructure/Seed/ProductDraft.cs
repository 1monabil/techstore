namespace TechStore.Infrastructure.Seed;

/// <summary>A product before it has an id; produced by catalog templates for both seeding paths.</summary>
public sealed class ProductDraft
{
    public string CategorySlug { get; set; } = default!;
    public string Brand { get; set; } = default!;
    public string Name { get; set; } = default!;
    public decimal Price { get; set; }
    public decimal? DiscountPrice { get; set; }
    public int StockQuantity { get; set; }
    public decimal Rating { get; set; }
    public int ReviewCount { get; set; }
    public int SalesCount { get; set; }
    public DateTime CreatedAt { get; set; }

    public int? RamGb { get; set; }
    public int? StorageGb { get; set; }
    public string? StorageType { get; set; }
    public string? CpuBrand { get; set; }
    public string? CpuModel { get; set; }
    public string? GpuBrand { get; set; }
    public string? GpuModel { get; set; }
    public decimal? ScreenSizeInches { get; set; }
    public int? RefreshRateHz { get; set; }
    public string? Color { get; set; }
    public string? Connectivity { get; set; }

    public Dictionary<string, string> Attributes { get; } = new(StringComparer.OrdinalIgnoreCase);

    /// <summary>Phrases the description generator weaves in (e.g. "OLED panel", "hot-swappable switches").</summary>
    public List<string> Highlights { get; } = [];
}
