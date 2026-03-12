using Aggregator.Aggregators.HackerNews;
using Microsoft.Extensions.Logging.Abstractions;
using NUnit.Framework;
using System.Net;

namespace Aggregator.Aggregators.Tests;

[TestFixture]
public class HackerNewsAggregatorTests
{
    [Test]
    public async Task FetchAsyncReturnsEmptyWhenHttpFails()
    {
        var factory = new StubHttpClientFactory(new FailingHttpMessageHandler());
        var aggregator = new HackerNewsAggregator(factory, NullLogger<HackerNewsAggregator>.Instance);

        var result = await aggregator.FetchAsync();

        Assert.That(result, Is.Empty);
    }

    [Test]
    public void NameIsHackernews()
    {
        var factory = new StubHttpClientFactory(new FailingHttpMessageHandler());
        var aggregator = new HackerNewsAggregator(factory, NullLogger<HackerNewsAggregator>.Instance);

        Assert.That(aggregator.Name, Is.EqualTo("hackernews"));
    }

    private sealed class StubHttpClientFactory(HttpMessageHandler handler) : IHttpClientFactory
    {
        public HttpClient CreateClient(string name) => new(handler);
    }

    private sealed class FailingHttpMessageHandler : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request,
            CancellationToken cancellationToken)
            => Task.FromResult(new HttpResponseMessage(HttpStatusCode.InternalServerError));
    }
}
