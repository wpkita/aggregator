namespace Aggregator.Aggregators.Abstractions;

[AttributeUsage(AttributeTargets.Class)]
public sealed class AggregatorPluginAttribute(string name, string displayName) : Attribute
{
    public string Name { get; } = name;
    public string DisplayName { get; } = displayName;
}
