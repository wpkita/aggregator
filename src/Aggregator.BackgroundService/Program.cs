using Aggregator.Aggregators.HackerNews;
using Aggregator.Core.Infrastructure;
using Aggregator.Data.Sqlite;
using Aggregator.Services;
using Aggregator.Worker;

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
using (var scope = host.Services.CreateScope())
{
    foreach (var aggregator in scope.ServiceProvider.GetServices<INewsAggregator>())
        registry.Register(aggregator);
}

await host.RunAsync();
