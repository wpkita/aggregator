using Aggregator.Aggregators.Dynamic;
using Aggregator.Core.Entities;
using Aggregator.Core.Infrastructure;
using Aggregator.Services;

namespace Aggregator.BackgroundService;

public class NewsWorker(
    IServiceProvider provider,
    IAggregatorRegistry registry,
    IConfiguration configuration,
    ILogger<NewsWorker> logger) : Microsoft.Extensions.Hosting.BackgroundService
{
    private static readonly Action<ILogger, Exception?> LogStarting =
        LoggerMessage.Define(
            LogLevel.Information,
            new EventId(1, "WorkerStarting"),
            "News polling worker starting");

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        LogStarting(logger, null);
        var pollInterval = TimeSpan.FromSeconds(
            configuration.GetValue("Worker:PollIntervalSeconds", 300));

        while (!stoppingToken.IsCancellationRequested)
        {
            using var scope = provider.CreateScope();
            await SyncDynamicAggregatorsAsync(scope.ServiceProvider, stoppingToken);

            var polling = scope.ServiceProvider.GetRequiredService<NewsPollingService>();
            await polling.PollAllAsync(stoppingToken);
            await Task.Delay(pollInterval, stoppingToken);
        }
    }

    private async Task SyncDynamicAggregatorsAsync(
        IServiceProvider services,
        CancellationToken cancellationToken)
    {
        var configRepo = services.GetRequiredService<IRepository<AggregatorConfig>>();
        var httpFactory = services.GetRequiredService<IHttpClientFactory>();
        var dynLogger = services.GetRequiredService<ILogger<DynamicAggregator>>();

        var configs = await configRepo.GetAllAsync(cancellationToken);
        var configNames = configs.Select(c => c.Name).ToHashSet();

        foreach (var config in configs)
        {
            if (!registry.IsRegistered(config.Name))
            {
                registry.Register(new DynamicAggregator(config, httpFactory, dynLogger));
            }
        }

        foreach (var aggregator in registry.GetAll().ToList())
        {
            if (aggregator is DynamicAggregator && !configNames.Contains(aggregator.Name))
            {
                registry.Unregister(aggregator.Name);
            }
        }
    }
}
