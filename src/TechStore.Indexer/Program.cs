using TechStore.Application;
using TechStore.Indexer;
using TechStore.Infrastructure;

// Usage:
//   dotnet run                → worker: initial index if the alias is missing, then drain the outbox forever
//   dotnet run -- --reindex   → one-off full reindex with alias swap, then exit
var builder = Host.CreateApplicationBuilder(args);
builder.Services.AddSearch(builder.Configuration);

var reindexOnly = args.Contains("--reindex");
if (!reindexOnly)
{
    builder.Services.AddHostedService<IndexerWorker>();
}

var host = builder.Build();

if (reindexOnly)
{
    var report = await host.Services.GetRequiredService<IReindexService>().ReindexAsync(CancellationToken.None);
    Console.WriteLine($"Reindexed {report.Documents:N0} documents into {report.IndexName} in {report.Elapsed}.");
    return;
}

await host.RunAsync();
