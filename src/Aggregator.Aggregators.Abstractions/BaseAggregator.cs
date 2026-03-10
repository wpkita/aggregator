using Aggregator.Core.Dtos;
using Aggregator.Core.Infrastructure;

namespace Aggregator.Aggregators.Abstractions;

public abstract class BaseAggregator : INewsAggregator
{
    public abstract string Name { get; }
    public abstract string DisplayName { get; }

    public abstract Task<IEnumerable<AggregatedNewsDto>> FetchAsync(
        CancellationToken cancellationToken = default);

    protected AggregatedNewsDto MapToDto(
        string title,
        string url,
        DateTime publishedAt,
        int? score = null,
        int? commentCount = null)
        => new(title, url, Name, publishedAt, score, commentCount);
}
