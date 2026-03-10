namespace Aggregator.Core.Entities;

public class AggregatorConfig
{
    public int Id { get; set; }
    public required string Name { get; set; }
    public required string DisplayName { get; set; }
    public required string Url { get; set; }
    public required string TitleField { get; set; }
    public required string UrlField { get; set; }
    public required string PublishedAtField { get; set; }
    public string? ScoreField { get; set; }
    public string? CommentCountField { get; set; }
}
