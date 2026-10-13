using Microsoft.AspNetCore.Diagnostics.HealthChecks;
using OpenTelemetry.Resources;
using OpenTelemetry.Trace;
using TechStore.Api.Endpoints;
using TechStore.Api.Infrastructure;
using TechStore.Application;
using TechStore.Infrastructure;
using TechStore.Infrastructure.Persistence;
using TechStore.Infrastructure.Search;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddApplication();
builder.Services.AddPersistence(builder.Configuration);
builder.Services.AddSearch(builder.Configuration);
builder.AddApiServices();

var otlp = builder.Configuration["OpenTelemetry:OtlpEndpoint"];
builder.Services.AddOpenTelemetry()
    .ConfigureResource(r => r.AddService("techstore-api"))
    .WithTracing(t =>
    {
        t.AddAspNetCoreInstrumentation(o => o.Filter = ctx => !ctx.Request.Path.StartsWithSegments("/health"))
         .AddHttpClientInstrumentation()
         .AddSqlClientInstrumentation()
         .AddSource(ElasticProductSearchService.Activity.Name, "Elastic.Transport");
        if (!string.IsNullOrWhiteSpace(otlp)) t.AddOtlpExporter(o => o.Endpoint = new Uri(otlp));
    });

var app = builder.Build();

using (var scope = app.Services.CreateScope())
{
    await scope.ServiceProvider.GetRequiredService<DatabaseInitializer>()
        .InitializeAsync(builder.Configuration.GetValue("Database:SeedOnStartup", false), app.Lifetime.ApplicationStopping);
}

app.UseExceptionHandler();
app.UseStatusCodePages();
app.UseResponseCompression();
app.UseCors();
app.UseRateLimiter();
app.UseOutputCache();

if (app.Environment.IsDevelopment())
{
    app.MapOpenApi();
    app.UseSwaggerUI(o => o.SwaggerEndpoint("/openapi/v1.json", "TechStore API"));
}

app.MapHealthChecks("/health", new HealthCheckOptions { Predicate = c => c.Tags.Contains("ready") });
app.MapHealthChecks("/health/live", new HealthCheckOptions { Predicate = _ => false });
app.MapTechStoreApi();

app.Run();

public partial class Program;
