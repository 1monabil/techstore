using FluentValidation;
using Microsoft.Extensions.DependencyInjection;
using TechStore.Application.Catalog;

namespace TechStore.Application;

public static class DependencyInjection
{
    public static IServiceCollection AddApplication(this IServiceCollection services)
    {
        services.AddValidatorsFromAssembly(typeof(DependencyInjection).Assembly, ServiceLifetime.Singleton);
        services.AddScoped<CatalogService>();
        return services;
    }
}
