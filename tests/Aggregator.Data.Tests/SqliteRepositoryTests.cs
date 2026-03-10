using Aggregator.Core.Entities;
using Aggregator.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using NUnit.Framework;

namespace Aggregator.Data.Tests;

[TestFixture]
public sealed class SqliteRepositoryTests : IDisposable
{
    private NewsContext _context = null!;
    private SqliteRepository<NewsItem> _repository = null!;

    [SetUp]
    public void SetUp()
    {
        var options = new DbContextOptionsBuilder<NewsContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;

        _context = new NewsContext(options);
        _repository = new SqliteRepository<NewsItem>(_context);
    }

    public void Dispose() => _context.Dispose();

    [Test]
    public async Task AddAsyncAndSaveChangesPersistsItem()
    {
        var item = new NewsItem
        {
            Title = "Test", Url = "https://example.com",
            Source = "test", PublishedAt = DateTime.UtcNow,
        };

        await _repository.AddAsync(item);
        await _repository.SaveChangesAsync();

        var all = await _repository.GetAllAsync();
        Assert.That(all.Count(), Is.EqualTo(1));
    }

    [Test]
    public async Task GetByIdAsyncReturnsNullWhenNotFound()
    {
        var result = await _repository.GetByIdAsync(999);
        Assert.That(result, Is.Null);
    }

    [Test]
    public async Task DeleteAsyncRemovesItem()
    {
        var item = new NewsItem
        {
            Title = "Test", Url = "https://example.com",
            Source = "test", PublishedAt = DateTime.UtcNow,
        };

        await _repository.AddAsync(item);
        await _repository.SaveChangesAsync();

        await _repository.DeleteAsync(item);
        await _repository.SaveChangesAsync();

        var all = await _repository.GetAllAsync();
        Assert.That(all, Is.Empty);
    }
}
