using Aggregator.Aggregators.Dynamic;
using Aggregator.Aggregators.HackerNews;
using Aggregator.Core.Entities;
using Aggregator.Core.Infrastructure;
using Aggregator.Data.Sqlite;
using Aggregator.Services;
using Aggregator.Worker;
using Microsoft.Extensions.Logging;

var builder = Host.CreateApplicationBuilder(args);

builder.Services.AddSqliteDataProvider(
    builder.Configuration.GetConnectionString("Default")
    ?? throw new InvalidOperationException(
        "ConnectionStrings:Default is not configured. " +
        "Set it in appsettings.json or via the ConnectionStrings__Default environment variable."));
builder.Services.AddNewsServices();
builder.Services.AddHackerNewsAggregator();
builder.Services.AddHostedService<NewsWorker>();

var host = builder.Build();

using (var scope = host.Services.CreateScope())
{
    var context = scope.ServiceProvider.GetRequiredService<NewsContext>();
    await context.Database.EnsureCreatedAsync();
}

var registry = host.Services.GetRequiredService<IAggregatorRegistry>();

// Register static aggregators
using (var scope = host.Services.CreateScope())
{
    foreach (INewsAggregator aggregator in scope.ServiceProvider.GetServices<INewsAggregator>())
    {
        registry.Register(aggregator);
    }
}

// Register persisted dynamic aggregators
using (var scope = host.Services.CreateScope())
{
    var configRepo = scope.ServiceProvider.GetRequiredService<IRepository<AggregatorConfig>>();
    var httpFactory = scope.ServiceProvider.GetRequiredService<IHttpClientFactory>();
    var dynLogger = scope.ServiceProvider.GetRequiredService<ILogger<DynamicAggregator>>();

    foreach (AggregatorConfig aggConfig in await configRepo.GetAllAsync())
    {
        registry.Register(new DynamicAggregator(aggConfig, httpFactory, dynLogger));
    }
}

await host.RunAsync();
