using System.Diagnostics;
using Elastic.Clients.Elasticsearch;
using Microsoft.Data.SqlClient;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using TechStore.Application;

namespace TechStore.Infrastructure.Search;

/// <summary>
/// Zero-downtime full reindex: new versioned index (replicas 0, refresh off) → stream SQL by keyset →
/// BulkAll (parallel, back-pressured) → restore settings → catch up rows changed meanwhile → atomic alias swap.
/// </summary>
public sealed class ReindexService(
    EsLowLevel es,
    IndexManager indices,
    ProductDocumentSource source,
    BulkWriter bulk,
    IConfiguration config,
    IOptions<IndexerOptions> options,
    TimeProvider clock,
    ILogger<ReindexService> log) : IReindexService
{
    private static readonly SemaphoreSlim Gate = new(1, 1);
    private readonly IndexerOptions _o = options.Value;

    public async Task<ReindexReport> ReindexAsync(CancellationToken ct)
    {
        if (!await Gate.WaitAsync(0, ct)) throw new InvalidOperationException("A reindex is already running in this process.");
        try { return await RunAsync(ct); }
        finally { Gate.Release(); }
    }

    private async Task<ReindexReport> RunAsync(CancellationToken ct)
    {
        var sw = Stopwatch.StartNew();
        // Rows updated after this instant are re-applied after the bulk load (see CatchUpAsync).
        var startedAt = clock.GetUtcNow().UtcDateTime.AddSeconds(-5);
        var name = indices.NewIndexName();
        await indices.CreateIndexAsync(name, bulkLoad: true, ct);

        try
        {
            var expected = await source.CountActiveAsync(ct);
            long indexed = 0, lastLogged = 0;
            var docs = source.StreamAll(_o.ReindexPageSize, ct: ct);

            var observable = es.Client.BulkAll(docs, b => b
                .Index(name)
                .Size(_o.BulkSize)
                .MaxDegreeOfParallelism(_o.BulkParallelism)
                .BackPressure(_o.BulkParallelism, 2)
                .BackOffRetries(5)
                .BackOffTime(TimeSpan.FromSeconds(2))
                .ContinueAfterDroppedDocuments(false)
                .RefreshOnCompleted(false), ct);

            await Task.Run(() => observable.Wait(TimeSpan.FromHours(6), r =>
            {
                var total = Interlocked.Add(ref indexed, r.Items.Count);
                if (total - Interlocked.Read(ref lastLogged) >= 100_000 || total == expected)
                {
                    Interlocked.Exchange(ref lastLogged, total);
                    log.LogInformation("Reindex {Index}: {Indexed:N0}/{Expected:N0} ({Rate:N0} docs/s)",
                        name, total, expected, total / Math.Max(1, sw.Elapsed.TotalSeconds));
                }
            }), ct);

            await indices.FinalizeAsync(name, ct);
            await CatchUpAsync(name, startedAt, ct);
            await indices.SwapAliasAsync(name, ct);

            log.LogInformation("Reindex complete: {Count:N0} docs into {Index} in {Elapsed}", indexed, name, sw.Elapsed);
            return new ReindexReport(name, indexed, sw.Elapsed);
        }
        catch
        {
            log.LogError("Reindex into {Index} failed; dropping the half-built index. The alias is untouched.", name);
            await indices.DeleteIndexAsync(name, CancellationToken.None);
            throw;
        }
    }

    /// <summary>
    /// While the bulk load ran, the outbox kept writing to the OLD index (via the alias).
    /// Re-apply every product touched since the start so the new index is not stale at swap time.
    /// </summary>
    private async Task CatchUpAsync(string index, DateTime since, CancellationToken ct)
    {
        var cs = config.GetConnectionString("Sql")!;
        var ids = new List<long>();
        await using (var conn = new SqlConnection(cs))
        {
            await conn.OpenAsync(ct);
            await using var cmd = new SqlCommand("SELECT Id FROM Products WHERE UpdatedAt >= @since", conn);
            cmd.Parameters.AddWithValue("@since", since);
            await using var r = await cmd.ExecuteReaderAsync(ct);
            while (await r.ReadAsync(ct)) ids.Add(r.GetInt64(0));
        }
        if (ids.Count == 0) return;

        foreach (var chunk in ids.Chunk(1000))
        {
            var docs = await source.LoadAsync(chunk, ct);
            var deletes = chunk.Where(id => !docs.ContainsKey(id)).ToList();
            await bulk.WriteAsync(index, docs.Values.ToList(), deletes, ct);
        }
        log.LogInformation("Reindex catch-up applied {Count} changed products", ids.Count);
    }
}
