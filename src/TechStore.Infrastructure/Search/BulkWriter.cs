using System.Text;
using System.Text.Json;
using Microsoft.Extensions.Logging;
using Polly;
using Polly.Retry;
using HttpMethod = Elastic.Transport.HttpMethod;

namespace TechStore.Infrastructure.Search;

/// <summary>Sends upserts/deletes as one _bulk request with exponential backoff on transient failures.</summary>
public sealed class BulkWriter
{
    private readonly EsLowLevel _es;
    private readonly ResiliencePipeline _retry;

    public BulkWriter(EsLowLevel es, ILogger<BulkWriter> log)
    {
        _es = es;
        _retry = new ResiliencePipelineBuilder()
            .AddRetry(new RetryStrategyOptions
            {
                MaxRetryAttempts = 5,
                BackoffType = DelayBackoffType.Exponential,
                UseJitter = true,
                Delay = TimeSpan.FromMilliseconds(500),
                ShouldHandle = new PredicateBuilder().Handle<EsException>(e => e.IsTransient).Handle<HttpRequestException>(),
                OnRetry = a =>
                {
                    log.LogWarning(a.Outcome.Exception, "Bulk request failed, retry {Attempt} in {Delay}", a.AttemptNumber + 1, a.RetryDelay);
                    return ValueTask.CompletedTask;
                },
            })
            .Build();
    }

    /// <returns>Per-document failures keyed by id (deletes of missing docs are not failures).</returns>
    public async Task<Dictionary<long, string>> WriteAsync(
        string index, IReadOnlyCollection<ProductDocument> upserts, IReadOnlyCollection<long> deletes, CancellationToken ct)
    {
        var failures = new Dictionary<long, string>();
        if (upserts.Count == 0 && deletes.Count == 0) return failures;

        var sb = new StringBuilder();
        foreach (var d in upserts)
        {
            sb.Append("{\"index\":{\"_id\":\"").Append(d.Id).Append("\"}}\n");
            sb.Append(JsonSerializer.Serialize(d)).Append('\n');
        }
        foreach (var id in deletes)
            sb.Append("{\"delete\":{\"_id\":\"").Append(id).Append("\"}}\n");

        var body = sb.ToString();
        using var resp = await _retry.ExecuteAsync(
            async token => await _es.SendAsync(HttpMethod.POST, $"/{index}/_bulk?filter_path=errors,items.*.status,items.*._id,items.*.error.reason", body, token),
            ct);

        if (!resp.RootElement.GetProperty("errors").GetBoolean()) return failures;
        foreach (var item in resp.RootElement.GetProperty("items").EnumerateArray())
        {
            var op = item.EnumerateObject().First();
            var status = op.Value.GetProperty("status").GetInt32();
            if (status < 300 || (op.Name == "delete" && status == 404)) continue;
            var id = long.Parse(op.Value.GetProperty("_id").GetString()!);
            failures[id] = op.Value.TryGetProperty("error", out var e) ? e.GetProperty("reason").GetString() ?? "error" : $"status {status}";
        }
        return failures;
    }
}
