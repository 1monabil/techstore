using System.Globalization;
using System.Text.Json;
using Microsoft.Data.SqlClient;
using Microsoft.Extensions.Configuration;

namespace TechStore.Infrastructure.Search;

/// <summary>
/// Reads products from SQL straight into <see cref="ProductDocument"/>s with raw ADO.NET:
/// keyset pagination for full reindex (never OFFSET), id lists for outbox batches.
/// </summary>
public sealed class ProductDocumentSource(IConfiguration config)
{
    private readonly string _cs = config.GetConnectionString("Sql")
        ?? throw new InvalidOperationException("ConnectionStrings:Sql is not configured.");

    private const string Select = """
        SELECT p.Id, p.Sku, p.Name, p.Slug, p.ShortDescription, p.Description,
               b.Name AS BrandName, b.Slug AS BrandSlug, c.Name AS CategoryName, c.Slug AS CategorySlug, c.Path AS CategoryPath,
               p.Price, p.DiscountPrice, p.StockQuantity, p.Rating, p.ReviewCount, p.SalesCount, p.CreatedAt,
               p.RamGb, p.StorageGb, p.StorageType, p.CpuBrand, p.CpuModel, p.GpuBrand, p.GpuModel,
               p.ScreenSizeInches, p.RefreshRateHz, p.Color, p.Connectivity,
               (SELECT TOP (1) i.Url FROM ProductImages i WHERE i.ProductId = p.Id ORDER BY i.IsPrimary DESC, i.SortOrder) AS ImageUrl,
               (SELECT a.[Key] AS k, a.[Value] AS v FROM ProductAttributes a WHERE a.ProductId = p.Id FOR JSON PATH) AS Attrs
        FROM Products p
        JOIN Brands b ON b.Id = p.BrandId
        JOIN Categories c ON c.Id = p.CategoryId
        """;

    /// <summary>Streams all active products in id order, one keyset page at a time.</summary>
    public IEnumerable<ProductDocument> StreamAll(int pageSize, Action<long>? onPage = null, CancellationToken ct = default)
    {
        // Synchronous on purpose: BulkAll consumes an IEnumerable lazily and applies back-pressure,
        // so we only read the next page from SQL when ES is ready for more.
        long after = 0;
        using var conn = new SqlConnection(_cs);
        conn.Open();
        while (!ct.IsCancellationRequested)
        {
            using var cmd = new SqlCommand($"{Select} WHERE p.Id > @after AND p.IsActive = 1 ORDER BY p.Id OFFSET 0 ROWS FETCH NEXT @take ROWS ONLY", conn)
            {
                CommandTimeout = 120,
            };
            cmd.Parameters.AddWithValue("@after", after);
            cmd.Parameters.AddWithValue("@take", pageSize);
            var count = 0;
            using (var reader = cmd.ExecuteReader())
            {
                while (reader.Read())
                {
                    var doc = Map(reader);
                    after = doc.Id;
                    count++;
                    yield return doc;
                }
            }
            onPage?.Invoke(after);
            if (count < pageSize) yield break;
        }
    }

    /// <summary>Loads the current state of the given products. Missing/inactive ids are omitted.</summary>
    public async Task<Dictionary<long, ProductDocument>> LoadAsync(IReadOnlyCollection<long> ids, CancellationToken ct)
    {
        var result = new Dictionary<long, ProductDocument>(ids.Count);
        if (ids.Count == 0) return result;
        await using var conn = new SqlConnection(_cs);
        await conn.OpenAsync(ct);
        await using var cmd = new SqlCommand($"{Select} WHERE p.IsActive = 1 AND p.Id IN (SELECT CAST([value] AS bigint) FROM OPENJSON(@ids))", conn);
        cmd.Parameters.AddWithValue("@ids", JsonSerializer.Serialize(ids));
        await using var reader = await cmd.ExecuteReaderAsync(ct);
        while (await reader.ReadAsync(ct))
        {
            var doc = Map(reader);
            result[doc.Id] = doc;
        }
        return result;
    }

    public async Task<long> CountActiveAsync(CancellationToken ct)
    {
        await using var conn = new SqlConnection(_cs);
        await conn.OpenAsync(ct);
        await using var cmd = new SqlCommand("SELECT COUNT_BIG(*) FROM Products WHERE IsActive = 1", conn);
        return (long)(await cmd.ExecuteScalarAsync(ct))!;
    }

    internal static ProductDocument Map(SqlDataReader r)
    {
        string? S(string col) => r[col] is DBNull ? null : (string)r[col];
        int? I(string col) => r[col] is DBNull ? null : (int)r[col];

        var price = r.GetDecimal(r.GetOrdinal("Price"));
        var discount = r["DiscountPrice"] is DBNull ? (decimal?)null : (decimal)r["DiscountPrice"];
        var rating = (double)r.GetDecimal(r.GetOrdinal("Rating"));
        var reviews = r.GetInt32(r.GetOrdinal("ReviewCount"));
        var sales = r.GetInt32(r.GetOrdinal("SalesCount"));
        var stock = r.GetInt32(r.GetOrdinal("StockQuantity"));
        var name = r.GetString(r.GetOrdinal("Name"));
        var brand = r.GetString(r.GetOrdinal("BrandName"));
        var brandSlug = r.GetString(r.GetOrdinal("BrandSlug"));
        var category = r.GetString(r.GetOrdinal("CategoryName"));
        var categorySlug = r.GetString(r.GetOrdinal("CategorySlug"));
        var path = r.GetString(r.GetOrdinal("CategoryPath")).Split('/');

        var doc = new ProductDocument
        {
            Id = r.GetInt64(r.GetOrdinal("Id")),
            Sku = r.GetString(r.GetOrdinal("Sku")),
            Name = name,
            Slug = r.GetString(r.GetOrdinal("Slug")),
            ShortDescription = S("ShortDescription"),
            Description = S("Description"),
            Brand = brand,
            BrandSlug = brandSlug,
            BrandFacet = $"{brandSlug}|{brand}",
            Category = category,
            CategorySlug = categorySlug,
            CategoryFacet = $"{categorySlug}|{category}",
            CategoryPath = path,
            Price = discount ?? price,
            ListPrice = price,
            DiscountPrice = discount,
            Rating = rating,
            ReviewCount = reviews,
            Stock = stock,
            InStock = stock > 0,
            SalesCount = sales,
            Popularity = sales + 1,
            // Confidence-weighted rating: 4.8 from 3 reviews should not beat 4.6 from 3,000.
            RatingBoost = Math.Round(rating * Math.Log10(reviews + 10), 4) + 0.01,
            CreatedAt = DateTime.SpecifyKind(r.GetDateTime(r.GetOrdinal("CreatedAt")), DateTimeKind.Utc),
            RamGb = I("RamGb"),
            StorageGb = I("StorageGb"),
            StorageType = S("StorageType"),
            CpuBrand = S("CpuBrand"),
            CpuModel = S("CpuModel"),
            GpuBrand = S("GpuBrand"),
            GpuModel = S("GpuModel"),
            ScreenSize = r["ScreenSizeInches"] is DBNull ? null : (decimal)r["ScreenSizeInches"],
            RefreshRate = I("RefreshRateHz"),
            Color = S("Color"),
            Connectivity = S("Connectivity"),
            ImageUrl = S("ImageUrl") ?? $"/assets/parts/{categorySlug}.svg",
        };

        if (S("Attrs") is { } attrsJson)
        {
            foreach (var a in JsonSerializer.Deserialize<KeyValue[]>(attrsJson) ?? [])
                doc.Attrs[a.k] = a.v;
        }

        doc.SpecLine = SpecLine(doc);
        doc.Suggest = new CompletionInput
        {
            Input = SuggestInputs(name, brand).ToArray(),
            Weight = (int)Math.Min(int.MaxValue, Math.Log2(sales + 2) * 100 + rating * 10),
            Contexts = new() { ["category"] = [.. path, ProductQueryBuilder.AllContext] },
        };
        return doc;
    }

    /// <summary>Full name and, when it starts with the brand, the name without it, so "rog str" completes "ASUS ROG Strix…".</summary>
    internal static IEnumerable<string> SuggestInputs(string name, string brand)
    {
        yield return name;
        if (name.StartsWith(brand + " ", StringComparison.OrdinalIgnoreCase))
        {
            var rest = name[(brand.Length + 1)..];
            yield return rest;
        }
    }

    /// <summary>The compact "silkscreen" line printed on product cards.</summary>
    internal static string SpecLine(ProductDocument d)
    {
        var parts = new List<string>(5);
        if (d.ScreenSize is { } s) parts.Add($"{s.ToString("0.#", CultureInfo.InvariantCulture)}\"{(d.RefreshRate is { } hz ? $" {hz}Hz" : "")}");
        else if (d.RefreshRate is { } hz) parts.Add($"{hz}Hz");
        if (d.CpuModel is { } cpu && d.CategorySlug != "processors") parts.Add(ShortCpu(cpu));
        if (d.GpuModel is { } gpu) parts.Add(ShortGpu(gpu));
        if (d.RamGb is { } ram) parts.Add($"{ram}GB");
        if (d.StorageGb is { } st) parts.Add($"{Units.StorageLabel(st)} {d.StorageType}".Trim());
        if (d.Attrs.TryGetValue("Wattage", out var w)) parts.Add(w.Replace(" ", ""));
        if (d.Attrs.TryGetValue("Socket", out var socket)) parts.Add(socket);
        if (d.Attrs.TryGetValue("Cores", out var cores)) parts.Add($"{cores}C/{d.Attrs.GetValueOrDefault("Threads")}T");
        if (d.Connectivity is { } c && parts.Count < 3) parts.Add(c);
        return string.Join(" · ", parts.Take(5));
    }

    private static string ShortCpu(string cpu) => cpu
        .Replace("Intel Core ", "", StringComparison.Ordinal)
        .Replace("AMD Ryzen ", "Ryzen ", StringComparison.Ordinal)
        .Replace("Apple ", "", StringComparison.Ordinal)
        .Replace("Qualcomm Snapdragon ", "Snapdragon ", StringComparison.Ordinal);

    private static string ShortGpu(string gpu) => gpu
        .Replace("GeForce ", "", StringComparison.Ordinal)
        .Replace(" Laptop GPU", "", StringComparison.Ordinal)
        .Replace("Radeon ", "", StringComparison.Ordinal);

    private sealed record KeyValue(string k, string v);
}
