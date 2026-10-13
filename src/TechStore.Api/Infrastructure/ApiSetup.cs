using System.Text.Json;
using System.Text.Json.Serialization;
using System.Threading.RateLimiting;
using Microsoft.AspNetCore.Diagnostics;
using Microsoft.AspNetCore.ResponseCompression;
using Microsoft.Extensions.Diagnostics.HealthChecks;
using TechStore.Api.Endpoints;
using TechStore.Application.Catalog;
using TechStore.Application.Search;
using TechStore.Infrastructure.Search;
using HttpMethod = Elastic.Transport.HttpMethod;

namespace TechStore.Api.Infrastructure;

public static class RateLimits
{
    public const string Search = "search";
    public const string Autocomplete = "autocomplete";
}

public static class OutputCachePolicies
{
    public const string Autocomplete = "autocomplete";
    public const string Catalog = "catalog";
}

/// <summary>System.Text.Json source generation for every API payload (no reflection on hot paths).</summary>
[JsonSourceGenerationOptions(JsonSerializerDefaults.Web, DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull)]
[JsonSerializable(typeof(SearchResult))]
[JsonSerializable(typeof(AutocompleteResult))]
[JsonSerializable(typeof(ProductDetail))]
[JsonSerializable(typeof(IReadOnlyList<CategoryNode>))]
[JsonSerializable(typeof(HomePage))]
[JsonSerializable(typeof(ReindexAccepted))]
[JsonSerializable(typeof(Microsoft.AspNetCore.Mvc.ProblemDetails))]
[JsonSerializable(typeof(Microsoft.AspNetCore.Http.HttpValidationProblemDetails))]
internal sealed partial class ApiJsonContext : JsonSerializerContext;

public static class ApiSetup
{
    public static void AddApiServices(this WebApplicationBuilder builder)
    {
        var services = builder.Services;
        var config = builder.Configuration;

        services.ConfigureHttpJsonOptions(o => o.SerializerOptions.TypeInfoResolverChain.Insert(0, ApiJsonContext.Default));
        services.AddProblemDetails();
        services.AddExceptionHandler<SearchUnavailableHandler>();

        services.AddResponseCompression(o =>
        {
            o.EnableForHttps = true;
            o.Providers.Add<BrotliCompressionProvider>();
            o.Providers.Add<GzipCompressionProvider>();
        });
        services.Configure<BrotliCompressionProviderOptions>(o => o.Level = System.IO.Compression.CompressionLevel.Fastest);
        services.Configure<GzipCompressionProviderOptions>(o => o.Level = System.IO.Compression.CompressionLevel.Fastest);

        services.AddOutputCache(o =>
        {
            var acSeconds = config.GetValue("Autocomplete:OutputCacheSeconds", 30);
            o.AddPolicy(OutputCachePolicies.Autocomplete, p =>
            {
                if (acSeconds <= 0) p.NoCache(); // load tests measure the uncached path with this
                else p.Expire(TimeSpan.FromSeconds(acSeconds)).SetVaryByQuery("q", "category");
            });
            o.AddPolicy(OutputCachePolicies.Catalog, p => p.Expire(TimeSpan.FromSeconds(60)));
        });

        var limits = config.GetSection("RateLimiting");
        services.AddRateLimiter(o =>
        {
            o.RejectionStatusCode = StatusCodes.Status429TooManyRequests;
            o.AddPolicy(RateLimits.Search, ctx => PerClient(ctx, limits.GetValue("SearchPerSecond", 20)));
            o.AddPolicy(RateLimits.Autocomplete, ctx => PerClient(ctx, limits.GetValue("AutocompletePerSecond", 30)));
        });

        services.AddCors(o => o.AddDefaultPolicy(p => p
            .WithOrigins(config.GetSection("Cors:Origins").Get<string[]>() ?? [])
            .AllowAnyHeader()
            .WithMethods("GET", "POST")));

        services.AddHealthChecks()
            .AddDbContextCheck<TechStore.Infrastructure.Persistence.TechStoreDbContext>("sql", tags: ["ready"])
            .AddCheck<ElasticsearchHealthCheck>("elasticsearch", tags: ["ready"]);

        services.AddOpenApi();
    }

    private static RateLimitPartition<string> PerClient(HttpContext ctx, int perSecond) =>
        RateLimitPartition.GetTokenBucketLimiter(
            ctx.Connection.RemoteIpAddress?.ToString() ?? "unknown",
            _ => new TokenBucketRateLimiterOptions
            {
                TokenLimit = perSecond * 2,
                TokensPerPeriod = perSecond,
                ReplenishmentPeriod = TimeSpan.FromSeconds(1),
                QueueLimit = 0,
                AutoReplenishment = true,
            });
}

/// <summary>Turns ES outages into a clear 503 ProblemDetails instead of a generic 500.</summary>
internal sealed class SearchUnavailableHandler(IProblemDetailsService problems, ILogger<SearchUnavailableHandler> log) : IExceptionHandler
{
    public async ValueTask<bool> TryHandleAsync(HttpContext http, Exception exception, CancellationToken ct)
    {
        if (exception is OperationCanceledException && http.RequestAborted.IsCancellationRequested)
        {
            http.Response.StatusCode = 499; // client went away (e.g. a superseded keystroke); nothing to send
            return true;
        }
        if (exception is not EsException es) return false;

        log.LogError(exception, "Elasticsearch request failed");
        http.Response.StatusCode = StatusCodes.Status503ServiceUnavailable;
        return await problems.TryWriteAsync(new ProblemDetailsContext
        {
            HttpContext = http,
            ProblemDetails =
            {
                Status = StatusCodes.Status503ServiceUnavailable,
                Title = "Search is temporarily unavailable",
                Detail = es.Status is null ? "The search cluster did not respond. Try again in a moment." : "The search cluster returned an error. Try again in a moment.",
            },
        });
    }
}

internal sealed class ElasticsearchHealthCheck(EsLowLevel es) : IHealthCheck
{
    public async Task<HealthCheckResult> CheckHealthAsync(HealthCheckContext context, CancellationToken ct = default)
    {
        try
        {
            using var doc = await es.SendAsync(HttpMethod.GET, "/_cluster/health?timeout=2s", null, ct);
            var status = doc.RootElement.GetProperty("status").GetString();
            return status == "red" ? HealthCheckResult.Unhealthy("cluster status red") : HealthCheckResult.Healthy($"cluster status {status}");
        }
        catch (Exception ex)
        {
            return HealthCheckResult.Unhealthy("Elasticsearch unreachable", ex);
        }
    }
}
