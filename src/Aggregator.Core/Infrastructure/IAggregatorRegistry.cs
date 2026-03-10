namespace Aggregator.Core.Infrastructure;

public interface IAggregatorRegistry
{
    void Register(INewsAggregator aggregator);
    INewsAggregator? Find(string name);
    IEnumerable<INewsAggregator> GetAll();
    bool IsRegistered(string name);
}
