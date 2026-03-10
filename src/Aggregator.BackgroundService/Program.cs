using Aggregator.Aggregators.HackerNews;
using Aggregator.Core.Infrastructure;
using Aggregator.Data.Sqlite;
using Aggregator.Services;
using Aggregator.Worker;

var builder = Host.CreateApplicationBuilder(args);

string rawConnectionString = builder.Configuration.GetConnectionString("Default")
    ?? throw new InvalidOperationException(
        "ConnectionStrings:Default is not configured. " +
        "Set it in appsettings.json or via the ConnectionStrings__Default environment variable.");

// Resolve relative Data Source paths against the content root so the app
// works regardless of the working directory (e.g. dotnet run --project ...).
string connectionString = ResolveConnectionString(rawConnectionString, builder.Environment.ContentRootPath);

builder.Services.AddSqliteDataProvider(connectionString);
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

await host.RunAsync();

static string ResolveConnectionString(string connectionString, string contentRoot)
{
    const string prefix = "Data Source=";
    if (!connectionString.StartsWith(prefix, StringComparison.OrdinalIgnoreCase))
    {
        return connectionString;
    }

    string dataSource = connectionString[prefix.Length..];
    if (Path.IsPathRooted(dataSource))
    {
        return connectionString;
    }

    string resolved = Path.GetFullPath(dataSource, contentRoot);
    Directory.CreateDirectory(Path.GetDirectoryName(resolved)!);
    return prefix + resolved;
}
