using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using TechStore.Application;
using HttpMethod = Elastic.Transport.HttpMethod;

namespace TechStore.Infrastructure.Search;

/// <summary>
/// Brands and categories with live product counts, held in process and refreshed every few
/// minutes. They are tiny (tens of rows), so autocomplete matches them in memory instead of
/// running aggregations per keystroke.
/// </summary>
public sealed class ReferenceData(EsLowLevel es, IServiceScopeFactory scopes, IOptions<ElasticsearchOptions> options, TimeProvider clock, ILogger<ReferenceData> log)
{
    public sealed record Entry(string Slug, string Name, long Count, string[] Words);
    public sealed record Snapshot(IReadOnlyList<Entry> Brands, IReadOnlyList<Entry> Categories, IReadOnlyDictionary<string, long> CategoryCounts, DateTimeOffset LoadedAt);

    private static readonly TimeSpan Ttl = TimeSpan.FromMinutes(5);
    private readonly SemaphoreSlim _lock = new(1, 1);
    private Snapshot? _current;

    public async Task<Snapshot> GetAsync(CancellationToken ct)
    {
        var snap = _current;
        if (snap is not null && clock.GetUtcNow() - snap.LoadedAt < Ttl) return snap;

        if (snap is not null)
        {
            // Serve the stale copy and refresh in the background; never block a keystroke on a refresh.
            if (_lock.CurrentCount > 0) _ = Task.Run(() => RefreshAsync(CancellationToken.None), CancellationToken.None);
            return snap;
        }
        return await RefreshAsync(ct);
    }

    private async Task<Snapshot> RefreshAsync(CancellationToken ct)
    {
        await _lock.WaitAsync(ct);
        try
        {
            if (_current is { } fresh && clock.GetUtcNow() - fresh.LoadedAt < Ttl) return fresh;

            using var doc = await es.SendAsync(HttpMethod.POST, $"/{options.Value.Alias}/_search?request_cache=true",
                ProductQueryBuilder.CategoryCounts().ToJsonString(), ct);
            var aggs = doc.RootElement.GetProperty("aggregations");

            var catCounts = aggs.GetProperty("paths").GetProperty("buckets").EnumerateArray()
                .ToDictionary(b => b.GetProperty("key").GetString()!, b => b.GetProperty("doc_count").GetInt64());

            var brands = aggs.GetProperty("brands").GetProperty("buckets").EnumerateArray().Select(b =>
            {
                var (slug, name) = SplitFacet(b.GetProperty("key").GetString()!);
                return new Entry(slug, name, b.GetProperty("doc_count").GetInt64(), Words(name));
            }).ToList();

            using var scope = scopes.CreateScope();
            var cats = await scope.ServiceProvider.GetRequiredService<ICategoryRepository>().GetAllAsync(ct);
            var categories = cats
                .Select(c => new Entry(c.Slug, c.Name, catCounts.GetValueOrDefault(c.Slug), Words(c.Name)))
                .Where(e => e.Count > 0)
                .ToList();

            _current = new Snapshot(brands, categories, catCounts, clock.GetUtcNow());
            return _current;
        }
        catch (Exception ex) when (_current is not null && ex is not OperationCanceledException)
        {
            log.LogWarning(ex, "Reference data refresh failed; serving the previous snapshot");
            return _current;
        }
        finally
        {
            _lock.Release();
        }
    }

    public static (string Slug, string Name) SplitFacet(string key)
    {
        var i = key.IndexOf('|');
        return i < 0 ? (key, key) : (key[..i], key[(i + 1)..]);
    }

    private static string[] Words(string name) =>
        name.ToLowerInvariant().Split([' ', '-', '_', '.'], StringSplitOptions.RemoveEmptyEntries).Append(name.ToLowerInvariant().Replace(" ", "")).ToArray();

    /// <summary>
    /// Entries where any word starts with any query token (length ≥ 2), allowing a typo in
    /// longer tokens the same way ES AUTO fuzziness does: 1 edit from 4 chars, 2 from 7.
    /// </summary>
    public static IEnumerable<Entry> Match(IEnumerable<Entry> entries, string query)
    {
        var tokens = query.ToLowerInvariant().Split(' ', StringSplitOptions.RemoveEmptyEntries).Where(t => t.Length >= 2).ToArray();
        if (tokens.Length == 0) return [];
        return entries
            .Where(e => tokens.Any(t => e.Words.Any(w => PrefixMatches(t, w))))
            .OrderByDescending(e => e.Count);
    }

    internal static bool PrefixMatches(string token, string word)
    {
        if (word.StartsWith(token, StringComparison.Ordinal)) return true;
        var allowed = token.Length >= 7 ? 2 : token.Length >= 4 ? 1 : 0;
        if (allowed == 0 || token[0] != word[0]) return false; // prefix_length 1, like the ES queries
        var prefix = word.Length > token.Length ? word[..token.Length] : word;
        return OsaDistance(token, prefix) <= allowed;
    }

    /// <summary>Optimal string alignment distance (Levenshtein + adjacent transpositions).</summary>
    private static int OsaDistance(string a, string b)
    {
        var d = new int[a.Length + 1, b.Length + 1];
        for (var i = 0; i <= a.Length; i++) d[i, 0] = i;
        for (var j = 0; j <= b.Length; j++) d[0, j] = j;
        for (var i = 1; i <= a.Length; i++)
        for (var j = 1; j <= b.Length; j++)
        {
            var cost = a[i - 1] == b[j - 1] ? 0 : 1;
            d[i, j] = Math.Min(Math.Min(d[i - 1, j] + 1, d[i, j - 1] + 1), d[i - 1, j - 1] + cost);
            if (i > 1 && j > 1 && a[i - 1] == b[j - 2] && a[i - 2] == b[j - 1])
                d[i, j] = Math.Min(d[i, j], d[i - 2, j - 2] + 1);
        }
        return d[a.Length, b.Length];
    }
}
