using Microsoft.Extensions.Options;
using TechStore.Infrastructure;
using TechStore.Infrastructure.Search;

namespace TechStore.Indexer;

/// <summary>
/// Long-running sync loop: builds the index on first start, then alternates between
/// draining the outbox and serving reindex requests queued by the admin endpoint.
/// </summary>
public sealed class IndexerWorker(
    OutboxProcessor outbox,
    ReindexService reindex,
    IndexManager indices,
    ProductDocumentSource source,
    IOptions<IndexerOptions> options,
    ILogger<IndexerWorker> log) : BackgroundService
{
    private readonly IndexerOptions _o = options.Value;

    protected override async Task ExecuteAsync(CancellationToken ct)
    {
        await WaitForDependenciesAsync(ct);

        if (!await indices.AliasExistsAsync(ct))
        {
            log.LogInformation("Alias '{Alias}' not found; running the initial full index", indices.Alias);
            await reindex.ReindexAsync(ct);
        }

        while (!ct.IsCancellationRequested)
        {
            try
            {
                if (await outbox.ClaimReindexRequestAsync(ct) is { } requestId)
                {
                    string? error = null;
                    try { await reindex.ReindexAsync(ct); }
                    catch (Exception ex) when (ex is not OperationCanceledException) { error = ex.Message; log.LogError(ex, "Requested reindex failed"); }
                    await outbox.CompleteReindexRequestAsync(requestId, error, ct);
                    continue;
                }

                var handled = await outbox.ProcessBatchAsync(ct);
                if (handled < _o.OutboxBatchSize) await Task.Delay(_o.PollInterval, ct);
            }
            catch (OperationCanceledException) when (ct.IsCancellationRequested)
            {
                break;
            }
            catch (Exception ex)
            {
                log.LogError(ex, "Indexer loop error; backing off");
                await Task.Delay(TimeSpan.FromSeconds(5), ct);
            }
        }
    }

    private async Task WaitForDependenciesAsync(CancellationToken ct)
    {
        for (var attempt = 1; ; attempt++)
        {
            try
            {
                await indices.AliasExistsAsync(ct);
                await source.CountActiveAsync(ct); // SQL reachable and migrated
                return;
            }
            catch (Exception ex) when (attempt < 30 && !ct.IsCancellationRequested)
            {
                log.LogWarning("Waiting for SQL/Elasticsearch (attempt {Attempt}): {Message}", attempt, ex.Message);
                await Task.Delay(TimeSpan.FromSeconds(3), ct);
            }
        }
    }
}
