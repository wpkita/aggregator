using Aggregator.Aggregators.Dynamic;
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

var rawConnectionString = config.GetConnectionString("Default")
    ?? throw new InvalidOperationException(
        "ConnectionStrings:Default is not configured. " +
        "Set it in appsettings.json or via the ConnectionStrings__Default environment variable.");

// Resolve relative Data Source paths against the working directory so the app
// works regardless of how it is invoked (e.g. dotnet run --project ...).
var connectionString = ResolveConnectionString(rawConnectionString, Directory.GetCurrentDirectory());

var services = new ServiceCollection();
services.AddLogging(b => b.AddConsole().SetMinimumLevel(LogLevel.Warning));
services.AddSqliteDataProvider(connectionString);
services.AddNewsServices();
services.AddHackerNewsAggregator();
services.AddHttpClient();

var provider = services.BuildServiceProvider();

using (var scope = provider.CreateScope())
{
    var context = scope.ServiceProvider.GetRequiredService<NewsContext>();
    await context.Database.EnsureCreatedAsync();
}

var registry = provider.GetRequiredService<IAggregatorRegistry>();

// Register static aggregators
using (var scope = provider.CreateScope())
{
    foreach (var aggregator in scope.ServiceProvider.GetServices<INewsAggregator>())
    {
        registry.Register(aggregator);
    }
}

// Register persisted dynamic aggregators
using (var scope = provider.CreateScope())
{
    var configRepo = scope.ServiceProvider.GetRequiredService<IRepository<AggregatorConfig>>();
    var httpFactory = scope.ServiceProvider.GetRequiredService<IHttpClientFactory>();
    var dynLogger = scope.ServiceProvider.GetRequiredService<ILogger<DynamicAggregator>>();

    foreach (var aggConfig in await configRepo.GetAllAsync())
    {
        registry.Register(new DynamicAggregator(aggConfig, httpFactory, dynLogger));
    }
}

// --- CLI dispatch ---

if (args.Length == 0)
{
    PrintUsage();
    return 0;
}

return args[0] switch
{
    "top" => await RunTopAsync(args[1..], provider, registry),
    "list" => RunList(args[1..], registry),
    "add" => await RunAddAsync(args[1..], provider, registry),
    "remove" => await RunRemoveAsync(args[1..], provider, registry),
    "--help" or "-h" => PrintHelp(),
    _ => PrintUnknownCommand(args[0]),
};

// --- Commands ---

static void PrintUsage()
{
    Console.WriteLine("aggy");
    Console.WriteLine();
    Console.WriteLine("Usage: aggy <command> [options]");
    Console.WriteLine();
    Console.WriteLine("Commands:");
    Console.WriteLine("  top       Display the top 3 items from each aggregator");
    Console.WriteLine("  list      List aggregators added by the user");
    Console.WriteLine("  add       Add a new aggregator");
    Console.WriteLine("  remove    Remove an aggregator");
    Console.WriteLine();
    Console.WriteLine("Run 'aggy --help' for more information.");
}

static int PrintHelp()
{
    Console.WriteLine("aggy - CLI news aggregator");
    Console.WriteLine();
    Console.WriteLine("Usage: aggy <command> [options]");
    Console.WriteLine();
    Console.WriteLine("Commands:");
    Console.WriteLine("  top                     Display the top 3 items from each aggregator");
    Console.WriteLine("  list                    List aggregators added by the user");
    Console.WriteLine("  add <url> [options]     Add a new aggregator");
    Console.WriteLine("  remove <name>           Remove a dynamic aggregator by name");
    Console.WriteLine();
    Console.WriteLine("Options:");
    Console.WriteLine("  --help, -h              Show help");
    Console.WriteLine();
    Console.WriteLine("Run 'aggy <command> --help' for command-specific help.");
    return 0;
}

static int PrintUnknownCommand(string command)
{
    Console.Error.WriteLine($"Unknown command: '{command}'");
    Console.Error.WriteLine("Run 'aggy --help' for usage.");
    return 1;
}

static async Task<int> RunTopAsync(
    string[] args,
    IServiceProvider provider,
    IAggregatorRegistry registry)
{
    if (args.Length > 0 && (args[0] == "--help" || args[0] == "-h"))
    {
        Console.WriteLine("Usage: aggy top");
        Console.WriteLine();
        Console.WriteLine("Fetches and displays the top 3 items from each registered aggregator.");
        return 0;
    }

    using var scope = provider.CreateScope();

    var repository = scope.ServiceProvider.GetRequiredService<IRepository<NewsItem>>();
    var allItems = await repository.GetAllAsync();

    foreach (var aggregator in registry.GetAll())
    {
        Console.WriteLine($"=== {aggregator.DisplayName} ===");
        var top = allItems
            .Where(x => x.Source == aggregator.Name)
            .OrderByDescending(x => x.Score)
            .Take(3)
            .ToList();

        if (top.Count > 0)
        {
            foreach (var item in top)
            {
                Console.WriteLine($"[{item.Source}] Score: {item.Score} | {item.Title}");
                Console.WriteLine($"  {item.Url}");
            }
        }
        else
        {
            Console.WriteLine("  No items yet.");
        }

        Console.WriteLine();
    }

    return 0;
}

static int RunList(string[] args, IAggregatorRegistry registry)
{
    if (args.Length > 0 && (args[0] == "--help" || args[0] == "-h"))
    {
        Console.WriteLine("Usage: aggy list");
        Console.WriteLine();
        Console.WriteLine("Lists all aggregators that have been added by the user.");
        return 0;
    }

    var aggregators = registry.GetAll().ToList();

    if (aggregators.Count == 0)
    {
        Console.WriteLine("No aggregators registered.");
        return 0;
    }

    Console.WriteLine($"{"Name",-20} {"Display Name",-30}");
    Console.WriteLine(new string('-', 52));

    foreach (var agg in aggregators)
    {
        Console.WriteLine($"{agg.Name,-20} {agg.DisplayName,-30}");
    }

    return 0;
}

static async Task<int> RunAddAsync(
    string[] args,
    IServiceProvider provider,
    IAggregatorRegistry registry)
{
    if (args.Length > 0 && (args[0] == "--help" || args[0] == "-h"))
    {
        Console.WriteLine("Usage: aggy add <url> [options]");
        Console.WriteLine();
        Console.WriteLine("Arguments:");
        Console.WriteLine("  <url>                        URL of the aggregator data (required)");
        Console.WriteLine();
        Console.WriteLine("Options:");
        Console.WriteLine("  -n, --name <name>            Internal name / source key (required)");
        Console.WriteLine("  -d, --display-name <name>    Display name (required)");
        Console.WriteLine("  -t, --title <field>          JSON field mapped to Title (required)");
        Console.WriteLine("  -u, --url <field>            JSON field mapped to Url (required)");
        Console.WriteLine("  -p, --published-at <field>   JSON field mapped to PublishedAt (required)");
        Console.WriteLine("  -s, --score <field>          JSON field mapped to Score (optional)");
        Console.WriteLine("  -c, --comment-count <field>  JSON field mapped to CommentCount (optional)");
        return 0;
    }

    if (args.Length == 0 || args[0].StartsWith('-'))
    {
        Console.Error.WriteLine("Error: <url> is required as the first argument.");
        Console.Error.WriteLine("Run 'aggy add --help' for usage.");
        return 1;
    }

    var url = args[0];
    var flags = ParseFlags(args[1..]);

    var name = GetFlag(flags, "-n", "--name");
    var displayName = GetFlag(flags, "-d", "--display-name");
    var titleField = GetFlag(flags, "-t", "--title");
    var urlField = GetFlag(flags, "-u", "--url");
    var publishedAtField = GetFlag(flags, "-p", "--published-at");
    var scoreField = GetFlag(flags, "-s", "--score");
    var commentCountField = GetFlag(flags, "-c", "--comment-count");

    var missing = new List<string>();
    if (name is null)
    { missing.Add("-n/--name"); }
    if (displayName is null)
    { missing.Add("-d/--display-name"); }
    if (titleField is null)
    { missing.Add("-t/--title"); }
    if (urlField is null)
    { missing.Add("-u/--url"); }
    if (publishedAtField is null)
    { missing.Add("-p/--published-at"); }

    if (missing.Count > 0)
    {
        Console.Error.WriteLine($"Error: Missing required option(s): {string.Join(", ", missing)}");
        Console.Error.WriteLine("Run 'aggy add --help' for usage.");
        return 1;
    }

    if (registry.IsRegistered(name!))
    {
        Console.Error.WriteLine($"Error: An aggregator named '{name}' is already registered.");
        return 1;
    }

    var aggConfig = new AggregatorConfig
    {
        Name = name!,
        DisplayName = displayName!,
        Url = url,
        TitleField = titleField!,
        UrlField = urlField!,
        PublishedAtField = publishedAtField!,
        ScoreField = scoreField,
        CommentCountField = commentCountField,
    };

    using var scope = provider.CreateScope();
    var configRepo = scope.ServiceProvider.GetRequiredService<IRepository<AggregatorConfig>>();
    await configRepo.AddAsync(aggConfig);
    await configRepo.SaveChangesAsync();

    var httpFactory = scope.ServiceProvider.GetRequiredService<IHttpClientFactory>();
    var dynLogger = scope.ServiceProvider.GetRequiredService<ILogger<DynamicAggregator>>();
    registry.Register(new DynamicAggregator(aggConfig, httpFactory, dynLogger));

    Console.WriteLine($"Added aggregator '{displayName}' ({name}).");
    return 0;
}

static async Task<int> RunRemoveAsync(
    string[] args,
    IServiceProvider provider,
    IAggregatorRegistry registry)
{
    if (args.Length > 0 && (args[0] == "--help" || args[0] == "-h"))
    {
        Console.WriteLine("Usage: aggy remove <name>");
        Console.WriteLine();
        Console.WriteLine("Arguments:");
        Console.WriteLine("  <name>    Internal name of the aggregator to remove (required)");
        Console.WriteLine();
        Console.WriteLine("Note: Static aggregators (e.g. hackernews) cannot be removed.");
        return 0;
    }

    if (args.Length == 0 || args[0].StartsWith('-'))
    {
        Console.Error.WriteLine("Error: <name> is required.");
        Console.Error.WriteLine("Run 'aggy remove --help' for usage.");
        return 1;
    }

    var name = args[0];

    using var scope = provider.CreateScope();
    var configRepo = scope.ServiceProvider.GetRequiredService<IRepository<AggregatorConfig>>();
    var all = await configRepo.GetAllAsync();
    var existing = all.FirstOrDefault(c => c.Name == name);

    if (existing is null)
    {
        Console.Error.WriteLine($"Error: No dynamic aggregator named '{name}' found.");
        Console.Error.WriteLine("Note: Static aggregators (e.g. hackernews) cannot be removed.");
        return 1;
    }

    await configRepo.DeleteAsync(existing);
    await configRepo.SaveChangesAsync();

    registry.Unregister(name);

    Console.WriteLine($"Removed aggregator '{name}'.");
    return 0;
}

// --- Argument parsing helpers ---

static Dictionary<string, string> ParseFlags(string[] args)
{
    var result = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);

    for (var i = 0; i < args.Length - 1; i++)
    {
        if (args[i].StartsWith('-') && !args[i + 1].StartsWith('-'))
        {
            result[args[i]] = args[i + 1];
            i++;
        }
    }

    return result;
}

static string? GetFlag(Dictionary<string, string> flags, string shortName, string longName)
    => flags.TryGetValue(shortName, out var v) ? v
        : flags.TryGetValue(longName, out v) ? v
        : null;

static string ResolveConnectionString(string connectionString, string baseDirectory)
{
    const string prefix = "Data Source=";
    if (!connectionString.StartsWith(prefix, StringComparison.OrdinalIgnoreCase))
    {
        return connectionString;
    }

    var dataSource = connectionString[prefix.Length..];
    if (Path.IsPathRooted(dataSource))
    {
        return connectionString;
    }

    var resolved = Path.GetFullPath(dataSource, baseDirectory);
    Directory.CreateDirectory(Path.GetDirectoryName(resolved)!);
    return prefix + resolved;
}
