using System.Globalization;
using System.Text.Json.Nodes;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using HttpMethod = Elastic.Transport.HttpMethod;

namespace TechStore.Infrastructure.Search;

/// <summary>Versioned indices behind a single alias; creation, finalisation and atomic swaps.</summary>
public sealed class IndexManager(EsLowLevel es, IOptions<ElasticsearchOptions> options, TimeProvider clock, ILogger<IndexManager> log)
{
    private readonly ElasticsearchOptions _o = options.Value;

    public string Alias => _o.Alias;

    public async Task<bool> AliasExistsAsync(CancellationToken ct) =>
        await es.StatusAsync(HttpMethod.HEAD, $"/_alias/{_o.Alias}", ct) == 200;

    public string NewIndexName() =>
        $"{_o.Alias}_v{clock.GetUtcNow().UtcDateTime.ToString("yyyyMMddHHmmssfff", CultureInfo.InvariantCulture)}";

    public async Task CreateIndexAsync(string name, bool bulkLoad, CancellationToken ct)
    {
        var definition = ProductIndexDefinition.Build(_o, LoadInlineSynonyms(), bulkLoad);
        await es.SendAsync(HttpMethod.PUT, $"/{name}", ProductIndexDefinition.ToJson(definition), ct);
        log.LogInformation("Created index {Index} (bulkLoad={BulkLoad})", name, bulkLoad);
    }

    /// <summary>Restores replicas/refresh after a bulk load and makes everything searchable.</summary>
    public async Task FinalizeAsync(string name, CancellationToken ct)
    {
        var body = new JsonObject
        {
            ["index"] = new JsonObject
            {
                ["refresh_interval"] = _o.RefreshInterval,
                ["number_of_replicas"] = _o.NumberOfReplicas,
            },
        };
        await es.SendAsync(HttpMethod.PUT, $"/{name}/_settings", body.ToJsonString(), ct);
        await es.SendAsync(HttpMethod.POST, $"/{name}/_refresh", null, ct);
        await ForceMergeAsync(name, ct);
        await es.SendAsync(HttpMethod.GET, $"/_cluster/health/{name}?wait_for_status=yellow&timeout=60s", null, ct);
    }

    /// <summary>
    /// A freshly bulk-loaded index has many segments; one segment searches measurably faster (the
    /// catalogue is read-mostly). Runs before the alias swap, so no live traffic is affected, and as a
    /// background task because a merge takes minutes, far longer than the client timeout.
    /// </summary>
    private async Task ForceMergeAsync(string name, CancellationToken ct)
    {
        using var started = await es.SendAsync(HttpMethod.POST, $"/{name}/_forcemerge?max_num_segments=1&wait_for_completion=false", null, ct);
        var task = started.RootElement.GetProperty("task").GetString();
        while (true)
        {
            // ES answers 429 (timeout_exception) when the wait elapses before the task finishes.
            using var status = await es.SendAsync(HttpMethod.GET, $"/_tasks/{task}?wait_for_completion=true&timeout=5s", null, ct, 408, 429);
            if (status.RootElement.TryGetProperty("completed", out var done) && done.GetBoolean()) break;
        }
        log.LogInformation("Force-merged {Index} to one segment", name);
    }

    /// <summary>Points the alias at <paramref name="newIndex"/> in one atomic _aliases call, then drops old versions.</summary>
    public async Task SwapAliasAsync(string newIndex, CancellationToken ct)
    {
        var old = await IndicesBehindAliasAsync(ct);
        var actions = new JsonArray();
        foreach (var idx in old.Where(i => i != newIndex))
            actions.Add(new JsonObject { ["remove"] = new JsonObject { ["index"] = idx, ["alias"] = _o.Alias } });
        actions.Add(new JsonObject { ["add"] = new JsonObject { ["index"] = newIndex, ["alias"] = _o.Alias } });
        await es.SendAsync(HttpMethod.POST, "/_aliases", new JsonObject { ["actions"] = actions }.ToJsonString(), ct);
        log.LogInformation("Alias {Alias} -> {Index} (was: {Old})", _o.Alias, newIndex, old.Count == 0 ? "none" : string.Join(",", old));

        foreach (var idx in old.Where(i => i != newIndex))
            await es.SendAsync(HttpMethod.DELETE, $"/{idx}", null, ct, 404);
    }

    public async Task DeleteIndexAsync(string name, CancellationToken ct) =>
        await es.SendAsync(HttpMethod.DELETE, $"/{name}", null, ct, 404);

    public async Task<IReadOnlyList<string>> IndicesBehindAliasAsync(CancellationToken ct)
    {
        using var doc = await es.SendAsync(HttpMethod.GET, $"/_alias/{_o.Alias}", null, ct, 404);
        return doc.RootElement.ValueKind == System.Text.Json.JsonValueKind.Object
            ? doc.RootElement.EnumerateObject().Where(p => p.Name != "error" && p.Name != "status").Select(p => p.Name).ToList()
            : [];
    }

    /// <summary>
    /// For hosts without the synonyms file mounted in ES config (tests), synonyms can be inlined.
    /// The repo file deploy/elasticsearch/synonyms.txt stays the single source either way.
    /// </summary>
    private IReadOnlyList<string>? LoadInlineSynonyms()
    {
        if (!_o.InlineSynonyms) return null;
        var path = FindSynonymsFile() ?? throw new FileNotFoundException("deploy/elasticsearch/synonyms.txt not found");
        return File.ReadAllLines(path)
            .Select(l => l.Trim())
            .Where(l => l.Length > 0 && !l.StartsWith('#'))
            .ToList();
    }

    private static string? FindSynonymsFile()
    {
        for (var dir = new DirectoryInfo(AppContext.BaseDirectory); dir is not null; dir = dir.Parent)
        {
            var candidate = Path.Combine(dir.FullName, "deploy", "elasticsearch", "synonyms.txt");
            if (File.Exists(candidate)) return candidate;
        }
        return null;
    }
}
