using Aggregator.Core.Infrastructure;
using Microsoft.Extensions.DependencyInjection;

namespace Aggregator.Services;

public static class ServiceCollectionExtensions
{
    public static IServiceCollection AddNewsServices(this IServiceCollection services)
    {
        services.AddSingleton<IAggregatorRegistry, AggregatorRegistry>();
        services.AddScoped<NewsPollingService>();
        return services;
    }
}
