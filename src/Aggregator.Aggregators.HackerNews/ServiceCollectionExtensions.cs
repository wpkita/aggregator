using Aggregator.Core.Infrastructure;
using Microsoft.Extensions.DependencyInjection;

namespace Aggregator.Aggregators.HackerNews;

public static class ServiceCollectionExtensions
{
    public static IServiceCollection AddHackerNewsAggregator(
        this IServiceCollection services)
    {
        services.AddHttpClient();
        services.AddScoped<INewsAggregator, HackerNewsAggregator>();
        return services;
    }
}
