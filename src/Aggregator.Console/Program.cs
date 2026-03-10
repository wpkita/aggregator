using Aggregator.Aggregators.HackerNews;
using Aggregator.Core.Infrastructure;
using Aggregator.Data.Sqlite;
using Aggregator.Services;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

var config = new ConfigurationBuilder()
    .AddJsonFile("appsettings.json", optional: true)
    .AddEnvironmentVariables()
    .Build();

var connectionString = config.GetConnectionString("Default")
    ?? throw new InvalidOperationException(
        "ConnectionStrings:Default is not configured. " +
        "Set it in appsettings.json or via the ConnectionStrings__Default environment variable.");

var services = new ServiceCollection();

services.AddLogging(b => b.AddConsole());
services.AddSqliteDataProvider(connectionString);
services.AddNewsServices();
services.AddHackerNewsAggregator();

var provider = services.BuildServiceProvider();

// Ensure database is created
using (var scope = provider.CreateScope())
{
    var context = scope.ServiceProvider.GetRequiredService<Aggregator.Data.Sqlite.NewsContext>();
    await context.Database.EnsureCreatedAsync();
}

// Register all INewsAggregator implementations with the registry
using (var scope = provider.CreateScope())
{
    var registry = provider.GetRequiredService<IAggregatorRegistry>();
    foreach (var aggregator in scope.ServiceProvider.GetServices<INewsAggregator>())
        registry.Register(aggregator);
}

// Poll
using (var scope = provider.CreateScope())
{
    var polling = scope.ServiceProvider.GetRequiredService<NewsPollingService>();
    await polling.PollAllAsync();
}
