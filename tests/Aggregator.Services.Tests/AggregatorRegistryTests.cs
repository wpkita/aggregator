using Aggregator.Core.Infrastructure;
using Aggregator.Services;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using NUnit.Framework;

namespace Aggregator.Services.Tests;

[TestFixture]
public class AggregatorRegistryTests
{
    private AggregatorRegistry _registry = null!;

    [SetUp]
    public void SetUp()
    {
        _registry = new AggregatorRegistry(NullLogger<AggregatorRegistry>.Instance);
    }

    [Test]
    public void RegisterAddsAggregatorWhenNotAlreadyRegistered()
    {
        var mock = BuildMock("test", "Test");

        _registry.Register(mock.Object);

        Assert.That(_registry.IsRegistered("test"), Is.True);
    }

    [Test]
    public void RegisterDoesNotDuplicateWhenCalledTwice()
    {
        var mock = BuildMock("test", "Test");
        _registry.Register(mock.Object);
        _registry.Register(mock.Object);

        Assert.That(_registry.GetAll().Count(), Is.EqualTo(1));
    }

    [Test]
    public void FindReturnsNullWhenNotRegistered()
    {
        Assert.That(_registry.Find("unknown"), Is.Null);
    }

    [Test]
    public void GetAllReturnsAllRegistered()
    {
        _registry.Register(BuildMock("a", "A").Object);
        _registry.Register(BuildMock("b", "B").Object);

        Assert.That(_registry.GetAll().Count(), Is.EqualTo(2));
    }

    [Test]
    public void UnregisterRemovesAggregatorWhenRegistered()
    {
        _registry.Register(BuildMock("test", "Test").Object);

        bool removed = _registry.Unregister("test");

        Assert.That(removed, Is.True);
        Assert.That(_registry.IsRegistered("test"), Is.False);
    }

    [Test]
    public void UnregisterReturnsFalseWhenNotRegistered()
    {
        bool removed = _registry.Unregister("nonexistent");

        Assert.That(removed, Is.False);
    }

    private static Mock<INewsAggregator> BuildMock(string name, string displayName)
    {
        var mock = new Mock<INewsAggregator>();
        mock.Setup(a => a.Name).Returns(name);
        mock.Setup(a => a.DisplayName).Returns(displayName);
        return mock;
    }
}
