using Aggregator.Core.Infrastructure;
using Aggregator.Services;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace Aggregator.Worker;

public class NewsWorker(
    IServiceProvider provider,
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
            var polling = scope.ServiceProvider.GetRequiredService<NewsPollingService>();

            await polling.PollAllAsync(stoppingToken);
            await Task.Delay(pollInterval, stoppingToken);
        }
    }
}
