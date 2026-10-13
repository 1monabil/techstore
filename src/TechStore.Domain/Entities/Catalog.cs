namespace TechStore.Domain.Entities;

public class Category
{
    public int Id { get; init; }
    public string Name { get; init; } = null!;
    public string Slug { get; init; } = null!;
    public int? ParentId { get; init; }
    public Category? Parent { get; init; }
    public List<Category> Children { get; init; } = [];
    public int SortOrder { get; init; }

    /// <summary>
    /// Materialized ancestor path of slugs, e.g. "components/graphics-cards".
    /// Indexed into ES as categoryPath so a filter on a parent includes all descendants.
    /// </summary>
    public string Path { get; init; } = null!;
}

public class Brand
{
    public int Id { get; init; }
    public string Name { get; init; } = null!;
    public string Slug { get; init; } = null!;
}

public class ProductAttribute
{
    public long Id { get; init; }
    public long ProductId { get; init; }
    public string Key { get; init; } = null!;
    public string Value { get; init; } = null!;
}

public class ProductImage
{
    public long Id { get; init; }
    public long ProductId { get; init; }
    public string Url { get; init; } = null!;
    public bool IsPrimary { get; init; }
    public int SortOrder { get; init; }
}

public class OutboxMessage
{
    public long Id { get; init; }
    public string Type { get; init; } = null!;
    public long AggregateId { get; init; }
    public DateTime OccurredAt { get; init; }
    public DateTime? ProcessedAt { get; init; }
    public int Attempts { get; init; }
    public string? LastError { get; init; }
    public bool DeadLettered { get; init; }
}

public static class OutboxMessageTypes
{
    public const string ProductUpserted = "ProductUpserted";
    public const string ProductDeleted = "ProductDeleted";
    public const string ReindexRequested = "ReindexRequested";
}
