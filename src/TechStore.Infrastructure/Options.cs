namespace TechStore.Infrastructure;

public sealed class ElasticsearchOptions
{
    public const string Section = "Elasticsearch";
    public string Url { get; set; } = "http://localhost:9200";
    public string? Username { get; set; }
    public string? Password { get; set; }
    /// <summary>Read/write alias. The API never addresses a concrete index.</summary>
    public string Alias { get; set; } = "products";
    /// <summary>
    /// Measured ~1.2 KB/doc → 10M docs ≈ 12 GB → 1 primary (target 20–40 GB). Under load on one node,
    /// 2 shards were slower (p95 343 ms vs 187 ms at 40 RPS). See README "Shard sizing".
    /// </summary>
    public int NumberOfShards { get; set; } = 1;
    public int NumberOfReplicas { get; set; } = 0;
    public string RefreshInterval { get; set; } = "1s";
    public string? SynonymsPath { get; set; } = "analysis/synonyms.txt";
    public bool InlineSynonyms { get; set; }
    public TimeSpan RequestTimeout { get; set; } = TimeSpan.FromSeconds(10);
}

public sealed class SearchOptions
{
    public const string Section = "Search";
    /// <summary>"fallback": exact first, fuzzy when few hits. "combined": one query, exact boosted.</summary>
    public string TypoStrategy { get; set; } = "fallback";
    public int FuzzyFallbackThreshold { get; set; } = 3;
    public int DidYouMeanThreshold { get; set; } = 5;
    public int TrackTotalHitsUpTo { get; set; } = 10_000;
    public string CursorSigningKey { get; set; } = "dev-only-change-me";
    public TimeSpan PitKeepAlive { get; set; } = TimeSpan.FromMinutes(2);
}

public sealed class IndexerOptions
{
    public const string Section = "Indexer";
    public int OutboxBatchSize { get; set; } = 500;
    public TimeSpan PollInterval { get; set; } = TimeSpan.FromSeconds(1);
    public int MaxAttempts { get; set; } = 8;
    public int ReindexPageSize { get; set; } = 5_000;
    public int BulkSize { get; set; } = 2_000;
    public int BulkParallelism { get; set; } = 4;
}
