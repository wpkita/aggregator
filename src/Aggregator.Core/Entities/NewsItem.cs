namespace Aggregator.Core.Entities;

public class NewsItem
{
    public int Id { get; set; }
    public required string Title { get; set; }
    public required string Url { get; set; }
    public required string Source { get; set; }
    public DateTime PublishedAt { get; set; }
    public int? Score { get; set; }
    public int? CommentCount { get; set; }
    public DateTime FetchedAt { get; set; } = DateTime.UtcNow;
}
