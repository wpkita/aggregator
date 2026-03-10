using Aggregator.Core.Dtos;

namespace Aggregator.Core.Infrastructure;

public interface INewsAggregator
{
    string Name { get; }
    string DisplayName { get; }
    Task<IEnumerable<AggregatedNewsDto>> FetchAsync(CancellationToken cancellationToken = default);
}
