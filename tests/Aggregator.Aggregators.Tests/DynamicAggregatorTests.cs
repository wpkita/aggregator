using Aggregator.Aggregators.Dynamic;
using Aggregator.Core.Entities;
using Microsoft.Extensions.Logging.Abstractions;
using NUnit.Framework;
using System.Net;
using System.Text;

namespace Aggregator.Aggregators.Tests;

[TestFixture]
public class DynamicAggregatorTests
{
    [Test]
    public void NameAndDisplayNameMatchConfig()
    {
        var config = MakeConfig();
        var aggregator = new DynamicAggregator(
            config,
            new StubHttpClientFactory(JsonResponse("[]")),
            NullLogger<DynamicAggregator>.Instance);

        Assert.That(aggregator.Name, Is.EqualTo("testfeed"));
        Assert.That(aggregator.DisplayName, Is.EqualTo("Test Feed"));
    }

    [Test]
    public async Task FetchAsyncReturnsEmptyWhenHttpFails()
    {
        var config = MakeConfig();
        var aggregator = new DynamicAggregator(
            config,
            new StubHttpClientFactory(new FailingHandler()),
            NullLogger<DynamicAggregator>.Instance);

        var result = await aggregator.FetchAsync();

        Assert.That(result, Is.Empty);
    }

    [Test]
    public async Task FetchAsyncMapsRootArrayItems()
    {
        const string json = """
            [
              { "headline": "Hello World", "link": "https://example.com/1", "ts": 1700000000, "pts": 42 }
            ]
            """;

        var config = MakeConfig(
            titleField: "headline",
            urlField: "link",
            publishedAtField: "ts",
            scoreField: "pts");

        var aggregator = new DynamicAggregator(
            config,
            new StubHttpClientFactory(JsonResponse(json)),
            NullLogger<DynamicAggregator>.Instance);

        var result = (await aggregator.FetchAsync()).ToList();

        Assert.That(result, Has.Count.EqualTo(1));
        Assert.That(result[0].Title, Is.EqualTo("Hello World"));
        Assert.That(result[0].Url, Is.EqualTo("https://example.com/1"));
        Assert.That(result[0].Score, Is.EqualTo(42));
        Assert.That(result[0].Source, Is.EqualTo("testfeed"));
    }

    [Test]
    public async Task FetchAsyncMapsNestedArrayItems()
    {
        const string json = """
            {
              "status": "ok",
              "items": [
                { "headline": "Nested Item", "link": "https://example.com/2", "ts": 1700000000 }
              ]
            }
            """;

        var config = MakeConfig(
            titleField: "headline",
            urlField: "link",
            publishedAtField: "ts");

        var aggregator = new DynamicAggregator(
            config,
            new StubHttpClientFactory(JsonResponse(json)),
            NullLogger<DynamicAggregator>.Instance);

        var result = (await aggregator.FetchAsync()).ToList();

        Assert.That(result, Has.Count.EqualTo(1));
        Assert.That(result[0].Title, Is.EqualTo("Nested Item"));
    }

    [Test]
    public async Task FetchAsyncUnwrapsRedditStyleItems()
    {
        const string json = """
            {
              "data": {
                "children": [
                  {
                    "kind": "t3",
                    "data": {
                      "title": "Reddit Post",
                      "url": "https://reddit.com/r/test",
                      "created_utc": 1700000000,
                      "score": 99,
                      "num_comments": 10
                    }
                  }
                ]
              }
            }
            """;

        var config = MakeConfig(
            titleField: "title",
            urlField: "url",
            publishedAtField: "created_utc",
            scoreField: "score",
            commentCountField: "num_comments");

        var aggregator = new DynamicAggregator(
            config,
            new StubHttpClientFactory(JsonResponse(json)),
            NullLogger<DynamicAggregator>.Instance);

        var result = (await aggregator.FetchAsync()).ToList();

        Assert.That(result, Has.Count.EqualTo(1));
        Assert.That(result[0].Title, Is.EqualTo("Reddit Post"));
        Assert.That(result[0].Score, Is.EqualTo(99));
        Assert.That(result[0].CommentCount, Is.EqualTo(10));
    }

    [Test]
    public async Task FetchAsyncSkipsItemsMissingTitleOrUrl()
    {
        const string json = """
            [
              { "headline": "No Link", "ts": 1700000000 },
              { "link": "https://example.com/noheadline", "ts": 1700000000 },
              { "headline": "Valid", "link": "https://example.com/valid", "ts": 1700000000 }
            ]
            """;

        var config = MakeConfig(
            titleField: "headline",
            urlField: "link",
            publishedAtField: "ts");

        var aggregator = new DynamicAggregator(
            config,
            new StubHttpClientFactory(JsonResponse(json)),
            NullLogger<DynamicAggregator>.Instance);

        var result = (await aggregator.FetchAsync()).ToList();

        Assert.That(result, Has.Count.EqualTo(1));
        Assert.That(result[0].Title, Is.EqualTo("Valid"));
    }

    [Test]
    public async Task FetchAsyncParsesIso8601DateTime()
    {
        const string json = """
            [
              {
                "headline": "ISO Date Post",
                "link": "https://example.com/iso",
                "published": "2023-11-14T12:00:00Z"
              }
            ]
            """;

        var config = MakeConfig(
            titleField: "headline",
            urlField: "link",
            publishedAtField: "published");

        var aggregator = new DynamicAggregator(
            config,
            new StubHttpClientFactory(JsonResponse(json)),
            NullLogger<DynamicAggregator>.Instance);

        var result = (await aggregator.FetchAsync()).ToList();

        Assert.That(result, Has.Count.EqualTo(1));
        Assert.That(result[0].PublishedAt, Is.EqualTo(new DateTime(2023, 11, 14, 12, 0, 0, DateTimeKind.Utc)));
    }

    [Test]
    public async Task FetchAsyncReturnsEmptyWhenNoArrayFound()
    {
        const string json = """{ "status": "ok" }""";

        var config = MakeConfig();
        var aggregator = new DynamicAggregator(
            config,
            new StubHttpClientFactory(JsonResponse(json)),
            NullLogger<DynamicAggregator>.Instance);

        var result = await aggregator.FetchAsync();

        Assert.That(result, Is.Empty);
    }

    // --- Helpers ---

    private static AggregatorConfig MakeConfig(
        string titleField = "title",
        string urlField = "url",
        string publishedAtField = "publishedAt",
        string? scoreField = null,
        string? commentCountField = null)
        => new()
        {
            Name = "testfeed",
            DisplayName = "Test Feed",
            Url = "https://example.com/api",
            TitleField = titleField,
            UrlField = urlField,
            PublishedAtField = publishedAtField,
            ScoreField = scoreField,
            CommentCountField = commentCountField,
        };

    private static StaticJsonHandler JsonResponse(string json)
        => new(json);

    private sealed class StubHttpClientFactory(HttpMessageHandler handler) : IHttpClientFactory
    {
        public HttpClient CreateClient(string name) => new(handler);
    }

    private sealed class StaticJsonHandler(string json) : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request,
            CancellationToken cancellationToken)
        {
            var response = new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent(json, Encoding.UTF8, "application/json"),
            };
            return Task.FromResult(response);
        }
    }

    private sealed class FailingHandler : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request,
            CancellationToken cancellationToken)
            => Task.FromResult(new HttpResponseMessage(HttpStatusCode.InternalServerError));
    }
}
