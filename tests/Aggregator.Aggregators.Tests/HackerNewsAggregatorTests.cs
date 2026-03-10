using System.Net;
using Aggregator.Aggregators.HackerNews;
using Microsoft.Extensions.Logging.Abstractions;
using NUnit.Framework;

namespace Aggregator.Aggregators.Tests;

[TestFixture]
public class HackerNewsAggregatorTests
{
    [Test]
    public async Task FetchAsyncReturnsEmptyWhenHttpFails()
    {
        var handler = new FailingHttpMessageHandler();
        var client = new HttpClient(handler)
        {
            BaseAddress = new Uri("https://hacker-news.firebaseio.com/")
        };

        var aggregator = new HackerNewsAggregator(client, NullLogger<HackerNewsAggregator>.Instance);

        var result = await aggregator.FetchAsync();

        Assert.That(result, Is.Empty);
    }

    [Test]
    public void NameIsHackernews()
    {
        var aggregator = new HackerNewsAggregator(
            new HttpClient(),
            NullLogger<HackerNewsAggregator>.Instance);

        Assert.That(aggregator.Name, Is.EqualTo("hackernews"));
    }

    private sealed class FailingHttpMessageHandler : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request,
            CancellationToken cancellationToken)
            => Task.FromResult(new HttpResponseMessage(HttpStatusCode.InternalServerError));
    }
}
