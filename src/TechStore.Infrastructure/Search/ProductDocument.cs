using System.Text.Json.Serialization;

namespace TechStore.Infrastructure.Search;

/// <summary>The ES read model of a product. Field names match <see cref="ProductIndexDefinition"/>.</summary>
public sealed class ProductDocument
{
    [JsonPropertyName("id")] public long Id { get; set; }
    [JsonPropertyName("sku")] public string Sku { get; set; } = default!;
    [JsonPropertyName("name")] public string Name { get; set; } = default!;
    [JsonPropertyName("slug")] public string Slug { get; set; } = default!;
    [JsonPropertyName("shortDescription")] public string? ShortDescription { get; set; }
    [JsonPropertyName("description")] public string? Description { get; set; }
    [JsonPropertyName("brand")] public string Brand { get; set; } = default!;
    [JsonPropertyName("brandSlug")] public string BrandSlug { get; set; } = default!;
    [JsonPropertyName("brandFacet")] public string BrandFacet { get; set; } = default!;
    [JsonPropertyName("category")] public string Category { get; set; } = default!;
    [JsonPropertyName("categorySlug")] public string CategorySlug { get; set; } = default!;
    [JsonPropertyName("categoryFacet")] public string CategoryFacet { get; set; } = default!;
    /// <summary>Slugs of the category and all its ancestors, so a parent filter matches descendants.</summary>
    [JsonPropertyName("categoryPath")] public string[] CategoryPath { get; set; } = [];
    [JsonPropertyName("price")] public decimal Price { get; set; }
    [JsonPropertyName("listPrice")] public decimal ListPrice { get; set; }
    [JsonPropertyName("discountPrice")] public decimal? DiscountPrice { get; set; }
    [JsonPropertyName("rating")] public double Rating { get; set; }
    [JsonPropertyName("reviewCount")] public int ReviewCount { get; set; }
    [JsonPropertyName("stock")] public int Stock { get; set; }
    [JsonPropertyName("inStock")] public bool InStock { get; set; }
    [JsonPropertyName("salesCount")] public int SalesCount { get; set; }
    [JsonPropertyName("popularity")] public double Popularity { get; set; }
    [JsonPropertyName("ratingBoost")] public double RatingBoost { get; set; }
    [JsonPropertyName("createdAt")] public DateTime CreatedAt { get; set; }

    [JsonPropertyName("ramGb"), JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)] public int? RamGb { get; set; }
    [JsonPropertyName("storageGb"), JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)] public int? StorageGb { get; set; }
    [JsonPropertyName("storageType"), JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)] public string? StorageType { get; set; }
    [JsonPropertyName("cpuBrand"), JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)] public string? CpuBrand { get; set; }
    [JsonPropertyName("cpuModel"), JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)] public string? CpuModel { get; set; }
    [JsonPropertyName("gpuBrand"), JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)] public string? GpuBrand { get; set; }
    [JsonPropertyName("gpuModel"), JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)] public string? GpuModel { get; set; }
    [JsonPropertyName("screenSize"), JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)] public decimal? ScreenSize { get; set; }
    [JsonPropertyName("refreshRate"), JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)] public int? RefreshRate { get; set; }
    [JsonPropertyName("color"), JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)] public string? Color { get; set; }
    [JsonPropertyName("connectivity"), JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)] public string? Connectivity { get; set; }
    [JsonPropertyName("attrs")] public Dictionary<string, string> Attrs { get; set; } = [];
    [JsonPropertyName("imageUrl")] public string? ImageUrl { get; set; }
    [JsonPropertyName("specLine")] public string? SpecLine { get; set; }
    [JsonPropertyName("suggest")] public CompletionInput Suggest { get; set; } = default!;
}

public sealed class CompletionInput
{
    [JsonPropertyName("input")] public string[] Input { get; set; } = [];
    [JsonPropertyName("weight")] public int Weight { get; set; }
    [JsonPropertyName("contexts")] public Dictionary<string, string[]> Contexts { get; set; } = [];
}
