using System.Text.Json;
using Elastic.Clients.Elasticsearch;
using Elastic.Transport;
using HttpMethod = Elastic.Transport.HttpMethod;

namespace TechStore.Infrastructure.Search;

public sealed class EsException(string message, int? status) : Exception(message)
{
    public int? Status { get; } = status;
    public bool IsTransient => Status is null or 429 or >= 500;
}

/// <summary>
/// Sends JSON requests through the official client's transport (same connection pool, retries
/// and diagnostics as the typed API). Queries are built as JSON so they read 1:1 with the ES
/// docs and can be snapshot-tested; the typed API is used where it adds value (BulkAll).
/// </summary>
public sealed class EsLowLevel(ElasticsearchClient client)
{
    public ElasticsearchClient Client => client;

    public async Task<JsonDocument> SendAsync(HttpMethod method, string path, string? body, CancellationToken ct, params int[] okStatuses)
    {
        var post = body is null ? null : PostData.String(body);
        var resp = await client.Transport.RequestAsync<BytesResponse>(method, path, post, ct);
        var status = resp.ApiCallDetails.HttpStatusCode;
        if (!resp.ApiCallDetails.HasSuccessfulStatusCode && (status is null || !okStatuses.Contains(status.Value)))
        {
            var text = resp.Body is { Length: > 0 } b ? System.Text.Encoding.UTF8.GetString(b) : resp.ApiCallDetails.OriginalException?.Message;
            throw new EsException($"ES {method} {path} failed ({status?.ToString() ?? "no response"}): {Truncate(text, 2000)}", status);
        }
        return resp.Body is { Length: > 0 } bytes ? JsonDocument.Parse(bytes) : JsonDocument.Parse("{}");
    }

    public async Task<int?> StatusAsync(HttpMethod method, string path, CancellationToken ct)
    {
        var resp = await client.Transport.RequestAsync<VoidResponse>(method, path, ct);
        return resp.ApiCallDetails.HttpStatusCode;
    }

    private static string? Truncate(string? s, int max) => s is null || s.Length <= max ? s : s[..max];
}
