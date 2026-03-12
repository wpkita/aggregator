using Aggregator.Core.Entities;
using Aggregator.Core.Infrastructure;
using Microsoft.Extensions.Logging;

namespace Aggregator.Services;

public class NewsPollingService(
    IAggregatorRegistry registry,
    IRepository<NewsItem> repository,
    ILogger<NewsPollingService> logger)
{
    private static readonly Action<ILogger, string, Exception?> LogAggregatorNotFound =
        LoggerMessage.Define<string>(
            LogLevel.Warning,
            new EventId(1, "AggregatorNotFound"),
            "Aggregator '{Name}' not found");

    private static readonly Action<ILogger, string, Exception?> LogPollingStarted =
        LoggerMessage.Define<string>(
            LogLevel.Information,
            new EventId(2, "PollingStarted"),
            "Polling {DisplayName}...");

    private static readonly Action<ILogger, string, Exception?> LogPollingFinished =
        LoggerMessage.Define<string>(
            LogLevel.Information,
            new EventId(3, "PollingFinished"),
            "Finished polling {DisplayName}");

    private static readonly Action<ILogger, string, Exception?> LogPollingError =
        LoggerMessage.Define<string>(
            LogLevel.Error,
            new EventId(4, "PollingError"),
            "Error polling {DisplayName}");

    public Task PollAllAsync(CancellationToken cancellationToken = default)
    {
        var tasks = registry.GetAll()
            .Select(agg => PollAggregatorAsync(agg, cancellationToken));
        return Task.WhenAll(tasks);
    }

    public async Task PollAsync(string aggregatorName, CancellationToken cancellationToken = default)
    {
        var aggregator = registry.Find(aggregatorName);
        if (aggregator is null)
        {
            LogAggregatorNotFound(logger, aggregatorName, null);
            return;
        }

        await PollAggregatorAsync(aggregator, cancellationToken);
    }

    private async Task PollAggregatorAsync(
        INewsAggregator aggregator,
        CancellationToken cancellationToken)
    {
        LogPollingStarted(logger, aggregator.DisplayName, null);

        try
        {
            var items = await aggregator.FetchAsync(cancellationToken);
            var existing = (await repository.GetAllAsync(cancellationToken))
                .ToDictionary(x => (x.Url, x.Source));

            foreach (var item in items)
            {
                if (existing.TryGetValue((item.Url, item.Source), out var existingItem))
                {
                    existingItem.Title = item.Title;
                    existingItem.PublishedAt = item.PublishedAt;
                    existingItem.Score = item.Score;
                    existingItem.CommentCount = item.CommentCount;
                    existingItem.FetchedAt = DateTime.UtcNow;
                    await repository.UpdateAsync(existingItem, cancellationToken);
                }
                else
                {
                    await repository.AddAsync(new NewsItem
                    {
                        Title = item.Title,
                        Url = item.Url,
                        Source = item.Source,
                        PublishedAt = item.PublishedAt,
                        Score = item.Score,
                        CommentCount = item.CommentCount,
                    }, cancellationToken);
                }
            }

            await repository.SaveChangesAsync(cancellationToken);
            LogPollingFinished(logger, aggregator.DisplayName, null);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            LogPollingError(logger, aggregator.DisplayName, ex);
        }
    }
}
