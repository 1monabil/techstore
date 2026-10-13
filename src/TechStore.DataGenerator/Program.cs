using System.Data;
using System.Diagnostics;
using Microsoft.Data.SqlClient;
using TechStore.DataGenerator;

// Usage: dotnet run -c Release -- --count 500000 --batch 50000 [--seed 42] [--connection "<sql>"]
// Idempotent and resumable: products get deterministic SKUs GEN-000000001…; a rerun continues
// after the last committed batch, and a completed run is a no-op.
var options = GeneratorOptions.Parse(args);
Console.WriteLine($"Target {options.Count:N0} generated products, batches of {options.Batch:N0}, seed {options.Seed}");

await using var conn = new SqlConnection(options.ConnectionString);
await conn.OpenAsync();

var reference = await ReferenceIds.LoadAsync(conn);
var existing = await CountGeneratedAsync(conn);
if (existing >= options.Count)
{
    Console.WriteLine($"Already have {existing:N0} generated products; nothing to do.");
    return;
}
Console.WriteLine(existing > 0 ? $"Resuming after {existing:N0} existing generated products" : "Starting from scratch");

var generator = new ProductGenerator(options.Seed, reference, DateTime.UtcNow);
var total = Stopwatch.StartNew();
long written = 0;

for (var start = existing; start < options.Count; start += options.Batch)
{
    var size = (int)Math.Min(options.Batch, options.Count - start);
    var sw = Stopwatch.StartNew();

    var firstId = await ReserveIdsAsync(conn, size);
    var (products, attributes) = generator.Build(start, size, firstId);

    await using (var tx = (SqlTransaction)await conn.BeginTransactionAsync())
    {
        await BulkCopyAsync(conn, tx, "Products", products);
        await BulkCopyAsync(conn, tx, "ProductAttributes", attributes);
        await tx.CommitAsync();
    }

    written += size;
    Console.WriteLine($"  {start + size,12:N0} / {options.Count:N0}  batch {size:N0} in {sw.Elapsed.TotalSeconds,5:0.0}s " +
                      $"({size / sw.Elapsed.TotalSeconds,8:N0} rows/s, {attributes.Rows.Count:N0} attrs)  overall {written / total.Elapsed.TotalSeconds:N0} rows/s");
}

Console.WriteLine($"Done: {written:N0} products in {total.Elapsed}. ({written / total.Elapsed.TotalSeconds:N0} rows/s)");
Console.WriteLine("SqlBulkCopy bypasses the outbox by design. Index the new rows with a full reindex:");
Console.WriteLine("  dotnet run --project src/TechStore.Indexer -- --reindex");

static async Task<long> CountGeneratedAsync(SqlConnection conn)
{
    await using var cmd = new SqlCommand("SELECT COUNT_BIG(*) FROM Products WHERE Sku LIKE 'GEN-%'", conn) { CommandTimeout = 300 };
    return (long)(await cmd.ExecuteScalarAsync())!;
}

/// <summary>
/// Reserves ids from the same HiLo sequence EF uses (INCREMENT BY 100, each value owns a block of
/// 100 ids), so generated rows can never collide with products created by the app.
/// </summary>
static async Task<long> ReserveIdsAsync(SqlConnection conn, int count)
{
    var blocks = (count + 99) / 100;
    await using var cmd = new SqlCommand("sp_sequence_get_range", conn) { CommandType = CommandType.StoredProcedure };
    cmd.Parameters.AddWithValue("@sequence_name", "ProductIds");
    cmd.Parameters.AddWithValue("@range_size", (long)blocks);
    var first = cmd.Parameters.Add("@range_first_value", SqlDbType.Variant);
    first.Direction = ParameterDirection.Output;
    await cmd.ExecuteNonQueryAsync();
    return Convert.ToInt64(first.Value);
}

static async Task BulkCopyAsync(SqlConnection conn, SqlTransaction tx, string table, DataTable rows)
{
    using var bulk = new SqlBulkCopy(conn, SqlBulkCopyOptions.TableLock | SqlBulkCopyOptions.CheckConstraints, tx)
    {
        DestinationTableName = table,
        BatchSize = 10_000,
        BulkCopyTimeout = 600,
        EnableStreaming = true,
    };
    foreach (DataColumn c in rows.Columns) bulk.ColumnMappings.Add(c.ColumnName, c.ColumnName);
    await bulk.WriteToServerAsync(rows);
}
