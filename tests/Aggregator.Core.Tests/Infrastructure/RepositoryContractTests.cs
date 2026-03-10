using Aggregator.Core.Infrastructure;
using Moq;
using NUnit.Framework;

namespace Aggregator.Core.Tests.Infrastructure;

[TestFixture]
public class RepositoryContractTests
{
    [Test]
    public async Task GetByIdAsyncReturnsNullWhenNotFound()
    {
        var mock = new Mock<IRepository<object>>();
        mock.Setup(r => r.GetByIdAsync(99, default)).ReturnsAsync((object?)null);

        object? result = await mock.Object.GetByIdAsync(99);

        Assert.That(result, Is.Null);
    }

    [Test]
    public async Task GetAllAsyncReturnsEmptyEnumerableWhenNoItems()
    {
        var mock = new Mock<IRepository<object>>();
        mock.Setup(r => r.GetAllAsync(default)).ReturnsAsync([]);

        IEnumerable<object> result = await mock.Object.GetAllAsync();

        Assert.That(result, Is.Empty);
    }
}
