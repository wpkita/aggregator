using System.Globalization;
using System.Net.Http.Json;
using System.Text;
using System.Text.Json.Serialization;
using Aggregator.Aggregators.Abstractions;
using Aggregator.Core.Dtos;
using Microsoft.Extensions.Logging;

namespace Aggregator.Aggregators.HackerNews;

[AggregatorPlugin("hackernews", "Hacker News")]
public class HackerNewsAggregator(
    IHttpClientFactory httpClientFactory,
    ILogger<HackerNewsAggregator> logger) : BaseAggregator
{
    private static readonly Action<ILogger, Exception?> LogFetchError =
        LoggerMessage.Define(
            LogLevel.Error,
            new EventId(1, "FetchError"),
            "Error fetching from HackerNews");

    private static readonly CompositeFormat ItemUrlFormat =
        CompositeFormat.Parse("https://hacker-news.firebaseio.com/v0/item/{0}.json");

    private const string TopStoriesUrl =
        "https://hacker-news.firebaseio.com/v0/topstories.json";
    private const int MaxStories = 30;

    public override string Name => "hackernews";
    public override string DisplayName => "Hacker News";

    public override async Task<IEnumerable<AggregatedNewsDto>> FetchAsync(
        CancellationToken cancellationToken = default)
    {
        try
        {
            using var httpClient = httpClientFactory.CreateClient();
            var topStories = await httpClient.GetFromJsonAsync<int[]>(
                TopStoriesUrl, cancellationToken);

            if (topStories is null)
                return [];

            var results = new List<AggregatedNewsDto>();

            foreach (var storyId in topStories.Take(MaxStories))
            {
                var story = await httpClient.GetFromJsonAsync<HackerNewsStory>(
                    string.Format(CultureInfo.InvariantCulture, ItemUrlFormat, storyId),
                    cancellationToken);

                if (story?.Url is not null)
                {
                    results.Add(MapToDto(
                        title: story.Title,
                        url: story.Url,
                        publishedAt: DateTimeOffset.FromUnixTimeSeconds(story.Time).UtcDateTime,
                        score: story.Score,
                        commentCount: story.Descendants));
                }
            }

            return results;
        }
        catch (Exception ex)
        {
            LogFetchError(logger, ex);
            return [];
        }
    }

    private sealed record HackerNewsStory(
        [property: JsonPropertyName("title")] string Title,
        [property: JsonPropertyName("url")] string? Url,
        [property: JsonPropertyName("score")] int Score,
        [property: JsonPropertyName("time")] long Time,
        [property: JsonPropertyName("descendants")] int Descendants
    );
}
