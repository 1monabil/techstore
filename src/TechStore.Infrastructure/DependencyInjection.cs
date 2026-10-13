using Elastic.Clients.Elasticsearch;
using Elastic.Transport;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using TechStore.Application;
using TechStore.Infrastructure.Persistence;
using TechStore.Infrastructure.Search;

namespace TechStore.Infrastructure;

public static class DependencyInjection
{
    public static IServiceCollection AddPersistence(this IServiceCollection services, IConfiguration config)
    {
        var cs = config.GetConnectionString("Sql")
                 ?? throw new InvalidOperationException("ConnectionStrings:Sql is not configured.");

        services.AddSingleton(TimeProvider.System);
        services.AddSingleton<OutboxInterceptor>();
        services.AddDbContext<TechStoreDbContext>((sp, o) => o
            .UseSqlServer(cs, sql => sql.EnableRetryOnFailure(3).CommandTimeout(60))
            .AddInterceptors(sp.GetRequiredService<OutboxInterceptor>()));

        services.AddScoped<DatabaseInitializer>();
        services.AddScoped<IProductRepository, ProductRepository>();
        services.AddScoped<ICategoryRepository, CategoryRepository>();
        services.AddScoped<IReindexRequestQueue, ReindexRequestQueue>();
        return services;
    }

    public static IServiceCollection AddSearch(this IServiceCollection services, IConfiguration config)
    {
        services.AddSingleton(TimeProvider.System);
        services.Configure<ElasticsearchOptions>(config.GetSection(ElasticsearchOptions.Section));
        services.Configure<SearchOptions>(config.GetSection(SearchOptions.Section));
        services.Configure<IndexerOptions>(config.GetSection(IndexerOptions.Section));

        // One client per process: it owns the HTTP connection pool.
        services.AddSingleton(sp =>
        {
            var o = sp.GetRequiredService<IOptions<ElasticsearchOptions>>().Value;
            var settings = new ElasticsearchClientSettings(new Uri(o.Url))
                .DefaultIndex(o.Alias)
                .RequestTimeout(o.RequestTimeout)
                .EnableHttpCompression()
                .ThrowExceptions(false);
            if (!string.IsNullOrEmpty(o.Username))
                settings = settings.Authentication(new BasicAuthentication(o.Username, o.Password ?? ""));
            return new ElasticsearchClient(settings);
        });
        services.AddSingleton<EsLowLevel>();
        services.AddSingleton<IndexManager>();
        services.AddSingleton<ProductDocumentSource>();
        services.AddSingleton<BulkWriter>();
        services.AddSingleton<ReindexService>();
        services.AddSingleton<IReindexService>(sp => sp.GetRequiredService<ReindexService>());
        services.AddSingleton<OutboxProcessor>();
        services.AddSingleton<CursorCodec>();
        services.AddSingleton<ReferenceData>();
        services.AddSingleton<IProductSearchService, ElasticProductSearchService>();
        return services;
    }
}
