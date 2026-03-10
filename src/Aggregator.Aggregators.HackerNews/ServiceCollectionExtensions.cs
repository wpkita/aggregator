using Aggregator.Core.Infrastructure;
using Microsoft.Extensions.DependencyInjection;

namespace Aggregator.Aggregators.HackerNews;

public static class ServiceCollectionExtensions
{
    public static IServiceCollection AddHackerNewsAggregator(
        this IServiceCollection services)
    {
        services.AddHttpClient(Microsoft.Extensions.Options.Options.DefaultName, client =>
        {
            client.DefaultRequestHeaders.UserAgent.ParseAdd(
                "Mozilla/5.0 (X11; Linux x86_64; rv:134.0) Gecko/20100101 Firefox/134.0");
        });
        services.AddScoped<INewsAggregator, HackerNewsAggregator>();
        return services;
    }
}
