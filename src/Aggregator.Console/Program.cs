using Aggregator.Aggregators.HackerNews;
using Aggregator.Core.Entities;
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

string connectionString = config.GetConnectionString("Default")
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
    foreach (INewsAggregator aggregator in scope.ServiceProvider.GetServices<INewsAggregator>())
    {
        registry.Register(aggregator);
    }
}

// Poll then print top 3 items per aggregator ordered by Score descending
using (var scope = provider.CreateScope())
{
    var repository = scope.ServiceProvider.GetRequiredService<IRepository<NewsItem>>();
    var allItems = await repository.GetAllAsync();

    var registry = provider.GetRequiredService<IAggregatorRegistry>();
    foreach (var aggregator in registry.GetAll())
    {
        Console.WriteLine($"=== {aggregator.DisplayName} ===");
        var top3 = allItems
            .Where(x => x.Source == aggregator.Name)
            .OrderByDescending(x => x.Score)
            .Take(3);

        foreach (NewsItem item in top3)
        {
            Console.WriteLine($"[{item.Source}] Score: {item.Score} | {item.Title} | {item.Url}");
        }

        Console.WriteLine();
    }
}
