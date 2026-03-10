namespace Aggregator.Core.Infrastructure;

public interface IAggregatorRegistry
{
    void Register(INewsAggregator aggregator);
    bool Unregister(string name);
    INewsAggregator? Find(string name);
    IEnumerable<INewsAggregator> GetAll();
    bool IsRegistered(string name);
}
