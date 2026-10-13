using System.Data;
using Bogus;
using Microsoft.Data.SqlClient;
using TechStore.Infrastructure.Seed;

namespace TechStore.DataGenerator;

/// <summary>Brand/category ids from the database, keyed by name/slug.</summary>
public sealed record ReferenceIds(IReadOnlyDictionary<string, int> Brands, IReadOnlyDictionary<string, int> Categories)
{
    public static async Task<ReferenceIds> LoadAsync(SqlConnection conn)
    {
        async Task<Dictionary<string, int>> Load(string sql)
        {
            var map = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
            await using var cmd = new SqlCommand(sql, conn);
            await using var r = await cmd.ExecuteReaderAsync();
            while (await r.ReadAsync()) map[r.GetString(1)] = r.GetInt32(0);
            return map;
        }

        var brands = await Load("SELECT Id, Name FROM Brands");
        var categories = await Load("SELECT Id, Slug FROM Categories");
        if (brands.Count == 0 || categories.Count == 0)
            throw new InvalidOperationException("Reference data missing. Start the API once (it migrates and seeds) before generating.");
        return new ReferenceIds(brands, categories);
    }
}

/// <summary>
/// Turns the curated product lines into millions of plausible listings. Every row is a pure
/// function of (seed, index), which is what makes reruns resumable and reproducible.
/// </summary>
public sealed class ProductGenerator(int seed, ReferenceIds reference, DateTime now)
{
    // Rough share of a real hardware catalogue by category.
    private static readonly Dictionary<string, float> CategoryWeights = new()
    {
        ["laptops"] = 14, ["smartphones"] = 12, ["graphics-cards"] = 9, ["monitors"] = 8, ["ssds"] = 8,
        ["keyboards"] = 7, ["mice"] = 7, ["headsets"] = 6, ["ram"] = 6, ["tablets"] = 5, ["processors"] = 4,
        ["motherboards"] = 4, ["hdds"] = 3, ["power-supplies"] = 3, ["cases"] = 3, ["routers"] = 3, ["switches"] = 2,
    };

    // Marketplace-style listing variants; most listings have none.
    private static readonly (string Suffix, float Weight, decimal PriceFactor)[] Editions =
    [
        ("", 62, 1m), ("(2024)", 7, 0.97m), ("(2025)", 7, 1.02m), ("– Renewed", 6, 0.78m), ("– Open Box", 4, 0.86m),
        ("Bundle", 3, 1.08m), ("– US Version", 3, 1m), ("with 2-Year Protection Plan", 3, 1.07m),
        ("– Retail Box", 3, 1m), ("– Bulk Pack", 2, 0.94m),
    ];

    private readonly string[] _categorySlugs = CategoryWeights.Keys.ToArray();
    private readonly float[] _categoryWeights = CategoryWeights.Values.ToArray();
    private readonly Dictionary<string, ProductLine[]> _lines = CategoryWeights.Keys
        .ToDictionary(c => c, c => CatalogTemplates.ForCategory(c).ToArray());

    public (DataTable Products, DataTable Attributes) Build(long startIndex, int count, long firstId)
    {
        var products = NewProductsTable(count);
        var attributes = NewAttributesTable(count * 4);

        for (var i = 0; i < count; i++)
        {
            var index = startIndex + i + 1;
            var id = firstId + i;
            var rowSeed = unchecked((int)(seed * 1_000_003L + index * 7919L));
            var random = new Randomizer(rowSeed);
            var rng = new Random(rowSeed);

            var category = random.WeightedRandom(_categorySlugs, _categoryWeights);
            var line = random.ArrayElement(_lines[category]);
            var draft = line.Build(line.RandomChoice(rng));
            DraftFinisher.Finish(draft, rng, now);

            var edition = random.WeightedRandom(Editions, Editions.Select(e => e.Weight).ToArray());
            if (edition.Suffix.Length > 0) draft.Name = $"{draft.Name} {edition.Suffix}";
            // ±10% street-price spread around the line's price, then the edition's adjustment.
            draft.Price = Math.Max(9m, Math.Round(draft.Price * edition.PriceFactor * (decimal)(0.9 + rng.NextDouble() * 0.2), 0) - 0.01m);
            if (draft.DiscountPrice is not null)
                draft.DiscountPrice = Math.Round(draft.Price * (decimal)(0.8 + rng.NextDouble() * 0.15), 0) - 0.01m;

            var (shortText, longText) = DraftFinisher.Describe(draft, rng);
            var created = now.AddDays(-rng.Next(0, 1100)).AddMinutes(-rng.Next(0, 1440));

            products.Rows.Add(
                id, $"GEN-{index:D9}", draft.Name, $"{Slugs.From(draft.Name, 200)}-{id}",
                shortText, Compact(longText), reference.Brands[draft.Brand], reference.Categories[draft.CategorySlug],
                draft.Price, (object?)draft.DiscountPrice ?? DBNull.Value, "USD", draft.StockQuantity, draft.Rating,
                draft.ReviewCount, draft.SalesCount, true, created, created,
                N(draft.RamGb), N(draft.StorageGb), N(draft.StorageType), N(draft.CpuBrand), N(draft.CpuModel),
                N(draft.GpuBrand), N(draft.GpuModel), N(draft.ScreenSizeInches), N(draft.RefreshRateHz),
                N(draft.Color), N(draft.Connectivity));

            foreach (var (key, value) in draft.Attributes)
                attributes.Rows.Add(id, key, value);
        }
        return (products, attributes);
    }

    /// <summary>Keeps generated descriptions to the first two sentences: realistic, and lighter on Express's 10 GB cap.</summary>
    private static string Compact(string text)
    {
        var cut = 0;
        for (var n = 0; n < 2; n++)
        {
            var next = text.IndexOf(". ", cut, StringComparison.Ordinal);
            if (next < 0) return text;
            cut = next + 1;
        }
        return text[..cut];
    }

    private static object N<T>(T? v) where T : struct => v.HasValue ? v.Value : DBNull.Value;
    private static object N(string? v) => v is null ? DBNull.Value : v;

    private static DataTable NewProductsTable(int capacity)
    {
        var t = new DataTable("Products") { MinimumCapacity = capacity };
        void C<T>(string name) => t.Columns.Add(name, Nullable.GetUnderlyingType(typeof(T)) ?? typeof(T));
        C<long>("Id"); C<string>("Sku"); C<string>("Name"); C<string>("Slug"); C<string>("ShortDescription");
        C<string>("Description"); C<int>("BrandId"); C<int>("CategoryId"); C<decimal>("Price"); C<decimal?>("DiscountPrice");
        C<string>("Currency"); C<int>("StockQuantity"); C<decimal>("Rating"); C<int>("ReviewCount"); C<int>("SalesCount");
        C<bool>("IsActive"); C<DateTime>("CreatedAt"); C<DateTime>("UpdatedAt");
        C<int?>("RamGb"); C<int?>("StorageGb"); C<string>("StorageType"); C<string>("CpuBrand"); C<string>("CpuModel");
        C<string>("GpuBrand"); C<string>("GpuModel"); C<decimal?>("ScreenSizeInches"); C<int?>("RefreshRateHz");
        C<string>("Color"); C<string>("Connectivity");
        return t;
    }

    private static DataTable NewAttributesTable(int capacity)
    {
        var t = new DataTable("ProductAttributes") { MinimumCapacity = capacity };
        t.Columns.Add("ProductId", typeof(long));
        t.Columns.Add("Key", typeof(string));
        t.Columns.Add("Value", typeof(string));
        return t;
    }
}
