using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Data.SqlClient;
using Microsoft.Extensions.DependencyInjection;
using TechStore.Application;
using TechStore.Application.Search;
using TechStore.Infrastructure.Search;
using HttpMethod = Elastic.Transport.HttpMethod;

namespace TechStore.IntegrationTests;

/// <summary>
/// Runs the real API against the local SQL Server and Elasticsearch, isolated per run:
/// a throwaway database (TechStore_IT_xxxx) and a throwaway alias (it_xxxx_products), both
/// removed afterwards. Override the targets with TECHSTORE_TEST_SQL_SERVER / TECHSTORE_TEST_ES_URL.
/// </summary>
public sealed class SearchFixture : IAsyncLifetime
{
    private readonly string _id = Guid.NewGuid().ToString("N")[..8];
    private string Server => Environment.GetEnvironmentVariable("TECHSTORE_TEST_SQL_SERVER") ?? @".\SQLEXPRESS";
    private string EsUrl => Environment.GetEnvironmentVariable("TECHSTORE_TEST_ES_URL") ?? "http://localhost:9200";
    public string Database => $"TechStore_IT_{_id}";
    public string Alias => $"it_{_id}_products";
    public string ConnectionString => $"Server={Server};Database={Database};Integrated Security=True;TrustServerCertificate=True;Encrypt=False";

    public WebApplicationFactory<Program> Factory { get; private set; } = default!;
    public HttpClient Client { get; private set; } = default!;
    public IServiceProvider Services => Factory.Services;

    public static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web);

    /// <summary>Where the throwaway database files go (the instance default may be on a full drive).</summary>
    private static string? DataDir =>
        Environment.GetEnvironmentVariable("TECHSTORE_TEST_DB_DIR") ?? (Directory.Exists(@"D:\TechStoreData") ? @"D:\TechStoreData" : null);

    public async ValueTask InitializeAsync()
    {
        if (DataDir is { } dir)
        {
            await using var conn = new SqlConnection($"Server={Server};Database=master;Integrated Security=True;TrustServerCertificate=True;Encrypt=False");
            await conn.OpenAsync();
            await using var cmd = new SqlCommand($"""
                CREATE DATABASE [{Database}]
                  ON (NAME = [{Database}], FILENAME = '{Path.Combine(dir, Database + ".mdf")}', SIZE = 32MB)
                  LOG ON (NAME = [{Database}_log], FILENAME = '{Path.Combine(dir, Database + "_log.ldf")}', SIZE = 16MB);
                ALTER DATABASE [{Database}] SET RECOVERY SIMPLE;
                """, conn);
            await cmd.ExecuteNonQueryAsync();
        }

        // Program reads configuration while building the host, so settings go in as env vars.
        Environment.SetEnvironmentVariable("ConnectionStrings__Sql", ConnectionString);
        Environment.SetEnvironmentVariable("Elasticsearch__Url", EsUrl);
        Environment.SetEnvironmentVariable("Elasticsearch__Alias", Alias);
        Environment.SetEnvironmentVariable("Elasticsearch__InlineSynonyms", "true");
        Environment.SetEnvironmentVariable("Database__SeedOnStartup", "true");
        Environment.SetEnvironmentVariable("RateLimiting__SearchPerSecond", "10000");
        Environment.SetEnvironmentVariable("RateLimiting__AutocompletePerSecond", "10000");

        Factory = new WebApplicationFactory<Program>().WithWebHostBuilder(b => b.UseEnvironment("Development"));
        Client = Factory.CreateClient(); // starts the host: migrations + curated seed
        await Services.GetRequiredService<IReindexService>().ReindexAsync(CancellationToken.None);
        await DrainOutboxAsync(); // seed wrote outbox rows; consume them so tests start clean
    }

    public async Task DrainOutboxAsync()
    {
        var outbox = Services.GetRequiredService<OutboxProcessor>();
        while (await outbox.ProcessBatchAsync(CancellationToken.None) > 0) { }
        await RefreshAsync();
    }

    public async Task RefreshAsync()
    {
        using var _ = await Services.GetRequiredService<EsLowLevel>().SendAsync(HttpMethod.POST, $"/{Alias}/_refresh", null, CancellationToken.None);
    }

    public async Task<SearchResult> SearchAsync(string query)
    {
        var response = await Client.GetAsync($"/api/products/search?{query}");
        response.EnsureSuccessStatusCode();
        return (await response.Content.ReadFromJsonAsync<SearchResult>(Json))!;
    }

    public async ValueTask DisposeAsync()
    {
        try
        {
            var indices = await Services.GetRequiredService<IndexManager>().IndicesBehindAliasAsync(CancellationToken.None);
            foreach (var index in indices)
                await Services.GetRequiredService<IndexManager>().DeleteIndexAsync(index, CancellationToken.None);
        }
        finally
        {
            Client.Dispose();
            await Factory.DisposeAsync();
            SqlConnection.ClearAllPools();
            await using var conn = new SqlConnection($"Server={Server};Database=master;Integrated Security=True;TrustServerCertificate=True;Encrypt=False");
            await conn.OpenAsync();
            await using var cmd = new SqlCommand($"ALTER DATABASE [{Database}] SET SINGLE_USER WITH ROLLBACK IMMEDIATE; DROP DATABASE [{Database}];", conn);
            await cmd.ExecuteNonQueryAsync();
        }
    }
}

[CollectionDefinition(Name)]
public sealed class SearchCollection : ICollectionFixture<SearchFixture>
{
    public const string Name = "search";
}
