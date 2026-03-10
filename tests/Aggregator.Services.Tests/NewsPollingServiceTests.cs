using Aggregator.Core.Dtos;
using Aggregator.Core.Entities;
using Aggregator.Core.Infrastructure;
using Aggregator.Services;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using NUnit.Framework;

namespace Aggregator.Services.Tests;

[TestFixture]
public class NewsPollingServiceTests
{
    private Mock<IAggregatorRegistry> _registryMock = null!;
    private Mock<IRepository<NewsItem>> _repositoryMock = null!;
    private NewsPollingService _service = null!;

    [SetUp]
    public void SetUp()
    {
        _registryMock = new Mock<IAggregatorRegistry>();
        _repositoryMock = new Mock<IRepository<NewsItem>>();
        _service = new NewsPollingService(
            _registryMock.Object,
            _repositoryMock.Object,
            NullLogger<NewsPollingService>.Instance);
    }

    [Test]
    public async Task PollAllAsyncFetchesAndStoresNewItems()
    {
        var dto = new AggregatedNewsDto(
            "Test Title", "https://example.com", "test",
            DateTime.UtcNow, 100, 10);

        var aggregatorMock = new Mock<INewsAggregator>();
        aggregatorMock.Setup(a => a.Name).Returns("test");
        aggregatorMock.Setup(a => a.DisplayName).Returns("Test");
        aggregatorMock.Setup(a => a.FetchAsync(default)).ReturnsAsync([dto]);

        _registryMock.Setup(r => r.GetAll()).Returns([aggregatorMock.Object]);
        _repositoryMock.Setup(r => r.GetAllAsync(default)).ReturnsAsync([]);

        await _service.PollAllAsync();

        _repositoryMock.Verify(
            r => r.AddAsync(It.Is<NewsItem>(n => n.Url == dto.Url), default),
            Times.Once);
        _repositoryMock.Verify(r => r.SaveChangesAsync(default), Times.Once);
    }

    [Test]
    public async Task PollAllAsyncSkipsDuplicates()
    {
        var dto = new AggregatedNewsDto(
            "Test Title", "https://example.com", "test",
            DateTime.UtcNow, 100, 10);

        var existingItem = new NewsItem
        {
            Title = dto.Title,
            Url = dto.Url,
            Source = dto.Source,
            PublishedAt = dto.PublishedAt,
        };

        var aggregatorMock = new Mock<INewsAggregator>();
        aggregatorMock.Setup(a => a.Name).Returns("test");
        aggregatorMock.Setup(a => a.DisplayName).Returns("Test");
        aggregatorMock.Setup(a => a.FetchAsync(default)).ReturnsAsync([dto]);

        _registryMock.Setup(r => r.GetAll()).Returns([aggregatorMock.Object]);
        _repositoryMock.Setup(r => r.GetAllAsync(default)).ReturnsAsync([existingItem]);

        await _service.PollAllAsync();

        _repositoryMock.Verify(
            r => r.AddAsync(It.IsAny<NewsItem>(), default),
            Times.Never);
    }

    [Test]
    public async Task PollAsyncDoesNothingWhenAggregatorNotFound()
    {
        _registryMock.Setup(r => r.Find("missing")).Returns((INewsAggregator?)null);

        await _service.PollAsync("missing");

        _repositoryMock.Verify(r => r.AddAsync(It.IsAny<NewsItem>(), default), Times.Never);
    }
}
