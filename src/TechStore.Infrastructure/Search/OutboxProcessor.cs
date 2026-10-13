using Microsoft.Data.SqlClient;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using TechStore.Domain.Entities;

namespace TechStore.Infrastructure.Search;

/// <summary>
/// Drains the transactional outbox into ES. Rows are claimed with UPDLOCK + READPAST inside a
/// transaction, so any number of indexer instances can run without double-processing or blocking
/// each other. Messages are marked processed only after ES has acknowledged them.
/// </summary>
public sealed class OutboxProcessor(
    ProductDocumentSource source,
    BulkWriter bulk,
    IndexManager indices,
    IConfiguration config,
    IOptions<IndexerOptions> options,
    TimeProvider clock,
    ILogger<OutboxProcessor> log)
{
    private readonly IndexerOptions _o = options.Value;
    private readonly string _cs = config.GetConnectionString("Sql")!;

    private sealed record Message(long Id, string Type, long AggregateId, int Attempts);

    /// <returns>Number of messages handled (0 = outbox empty).</returns>
    public async Task<int> ProcessBatchAsync(CancellationToken ct)
    {
        await using var conn = new SqlConnection(_cs);
        await conn.OpenAsync(ct);
        await using var tx = (SqlTransaction)await conn.BeginTransactionAsync(ct);

        var batch = await ClaimAsync(conn, tx, ct);
        if (batch.Count == 0)
        {
            await tx.CommitAsync(ct);
            return 0;
        }

        var (failures, upserts, deletes) = await SyncToIndexAsync(batch, ct);
        var done = batch.Where(m => !failures.ContainsKey(m.AggregateId)).Select(m => m.Id).ToList();
        var failed = batch.Where(m => failures.ContainsKey(m.AggregateId)).ToList();
        await MarkProcessedAsync(conn, tx, done, ct);
        await RecordFailuresAsync(conn, tx, failed, failures, ct);

        await tx.CommitAsync(ct);
        log.LogInformation("Outbox batch: {Done} indexed, {Failed} failed ({Upserts} upserts, {Deletes} deletes)",
            done.Count, failed.Count, upserts, deletes);
        return batch.Count;
    }

    /// <returns>Per-product failures (empty when everything was indexed) and what was sent.</returns>
    private async Task<(Dictionary<long, string> Failures, int Upserts, int Deletes)> SyncToIndexAsync(
        IReadOnlyList<Message> batch, CancellationToken ct)
    {
        // Several messages for one product collapse into its latest state.
        var latest = batch.GroupBy(m => m.AggregateId).Select(g => g.MaxBy(m => m.Id)!).ToList();
        var upsertIds = latest.Where(m => m.Type == OutboxMessageTypes.ProductUpserted).Select(m => m.AggregateId).ToList();
        var deleteIds = latest.Where(m => m.Type == OutboxMessageTypes.ProductDeleted).Select(m => m.AggregateId).ToList();

        var docs = await source.LoadAsync(upsertIds, ct);
        deleteIds.AddRange(upsertIds.Where(id => !docs.ContainsKey(id))); // deactivated → remove from ES

        try
        {
            return (await bulk.WriteAsync(indices.Alias, docs.Values.ToList(), deleteIds, ct), docs.Count, deleteIds.Count);
        }
        catch (Exception ex) when (ex is EsException or HttpRequestException)
        {
            // ES still failing after BulkWriter's retries: count an attempt for every message and keep
            // them pending (dead-lettered after MaxAttempts). Anything else is a bug and propagates.
            var wholeBatch = batch.Select(m => m.AggregateId).Distinct().ToDictionary(id => id, _ => ex.Message);
            return (wholeBatch, docs.Count, deleteIds.Count);
        }
    }

    private async Task MarkProcessedAsync(SqlConnection conn, SqlTransaction tx, IReadOnlyList<long> messageIds, CancellationToken ct)
    {
        if (messageIds.Count == 0) return;
        await using var cmd = new SqlCommand(
            "UPDATE OutboxMessages SET ProcessedAt = @now WHERE Id IN (SELECT CAST([value] AS bigint) FROM OPENJSON(@ids))", conn, tx);
        cmd.Parameters.AddWithValue("@now", clock.GetUtcNow().UtcDateTime);
        cmd.Parameters.AddWithValue("@ids", System.Text.Json.JsonSerializer.Serialize(messageIds));
        await cmd.ExecuteNonQueryAsync(ct);
    }

    private async Task RecordFailuresAsync(
        SqlConnection conn, SqlTransaction tx, IReadOnlyList<Message> failed, IReadOnlyDictionary<long, string> errors, CancellationToken ct)
    {
        foreach (var m in failed)
        {
            var dead = m.Attempts + 1 >= _o.MaxAttempts;
            await using var cmd = new SqlCommand(
                "UPDATE OutboxMessages SET Attempts = Attempts + 1, LastError = @err, DeadLettered = @dead WHERE Id = @id", conn, tx);
            cmd.Parameters.AddWithValue("@err", Truncate(errors[m.AggregateId], 2000));
            cmd.Parameters.AddWithValue("@dead", dead);
            cmd.Parameters.AddWithValue("@id", m.Id);
            await cmd.ExecuteNonQueryAsync(ct);
            if (dead) log.LogError("Outbox message {Id} for product {ProductId} dead-lettered: {Error}", m.Id, m.AggregateId, errors[m.AggregateId]);
        }
    }

    private async Task<List<Message>> ClaimAsync(SqlConnection conn, SqlTransaction tx, CancellationToken ct)
    {
        await using var cmd = new SqlCommand("""
            SELECT TOP (@take) Id, Type, AggregateId, Attempts
            FROM OutboxMessages WITH (UPDLOCK, READPAST, ROWLOCK)
            WHERE ProcessedAt IS NULL AND DeadLettered = 0 AND Type IN (@upsert, @delete)
            ORDER BY Id
            """, conn, tx);
        cmd.Parameters.AddWithValue("@take", _o.OutboxBatchSize);
        cmd.Parameters.AddWithValue("@upsert", OutboxMessageTypes.ProductUpserted);
        cmd.Parameters.AddWithValue("@delete", OutboxMessageTypes.ProductDeleted);
        var list = new List<Message>();
        await using var r = await cmd.ExecuteReaderAsync(ct);
        while (await r.ReadAsync(ct)) list.Add(new Message(r.GetInt64(0), r.GetString(1), r.GetInt64(2), r.GetInt32(3)));
        return list;
    }

    /// <summary>Claims one pending reindex request (marks it so no other worker takes it).</summary>
    public async Task<long?> ClaimReindexRequestAsync(CancellationToken ct)
    {
        await using var conn = new SqlConnection(_cs);
        await conn.OpenAsync(ct);
        await using var cmd = new SqlCommand("""
            UPDATE TOP (1) OutboxMessages WITH (UPDLOCK, READPAST, ROWLOCK)
            SET Attempts = Attempts + 1
            OUTPUT inserted.Id
            WHERE Type = @type AND ProcessedAt IS NULL AND DeadLettered = 0 AND Attempts = 0
            """, conn);
        cmd.Parameters.AddWithValue("@type", OutboxMessageTypes.ReindexRequested);
        return await cmd.ExecuteScalarAsync(ct) as long?;
    }

    public async Task CompleteReindexRequestAsync(long id, string? error, CancellationToken ct)
    {
        await using var conn = new SqlConnection(_cs);
        await conn.OpenAsync(ct);
        await using var cmd = new SqlCommand(
            "UPDATE OutboxMessages SET ProcessedAt = CASE WHEN @err IS NULL THEN @now END, LastError = @err, DeadLettered = CASE WHEN @err IS NULL THEN 0 ELSE 1 END WHERE Id = @id", conn);
        cmd.Parameters.AddWithValue("@now", clock.GetUtcNow().UtcDateTime);
        cmd.Parameters.AddWithValue("@err", (object?)error ?? DBNull.Value);
        cmd.Parameters.AddWithValue("@id", id);
        await cmd.ExecuteNonQueryAsync(ct);
    }

    private static string Truncate(string s, int max) => s.Length <= max ? s : s[..max];
}
