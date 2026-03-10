namespace Aggregator.Core.Dtos;

public record AggregatedNewsDto(
    string Title,
    string Url,
    string Source,
    DateTime PublishedAt,
    int? Score,
    int? CommentCount
);
