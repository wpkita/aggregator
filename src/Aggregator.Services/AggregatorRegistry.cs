using Aggregator.Core.Infrastructure;
using Microsoft.Extensions.Logging;

namespace Aggregator.Services;

public class AggregatorRegistry(ILogger<AggregatorRegistry> logger) : IAggregatorRegistry
{
    private static readonly Action<ILogger, string, Exception?> LogAlreadyRegistered =
        LoggerMessage.Define<string>(
            LogLevel.Warning,
            new EventId(1, nameof(Register)),
            "Aggregator '{Name}' is already registered");

    private static readonly Action<ILogger, string, Exception?> LogRegistered =
        LoggerMessage.Define<string>(
            LogLevel.Information,
            new EventId(2, nameof(Register)),
            "Registered aggregator: {DisplayName}");

    private readonly Dictionary<string, INewsAggregator> _aggregators = [];

    public void Register(INewsAggregator aggregator)
    {
        if (IsRegistered(aggregator.Name))
        {
            LogAlreadyRegistered(logger, aggregator.Name, null);
            return;
        }

        _aggregators[aggregator.Name] = aggregator;
        LogRegistered(logger, aggregator.DisplayName, null);
    }

    public INewsAggregator? Find(string name)
        => _aggregators.TryGetValue(name, out var agg) ? agg : null;

    public IEnumerable<INewsAggregator> GetAll() => _aggregators.Values;

    public bool IsRegistered(string name) => _aggregators.ContainsKey(name);
}
