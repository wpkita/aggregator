using Aggregator.Core.Entities;
using Aggregator.Core.Infrastructure;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace Aggregator.Data.Sqlite;

public static class ServiceCollectionExtensions
{
    public static IServiceCollection AddSqliteDataProvider(
        this IServiceCollection services,
        string connectionString)
    {
        services.AddDbContext<NewsContext>(opt => opt.UseSqlite(connectionString));
        services.AddScoped<IRepository<NewsItem>, SqliteRepository<NewsItem>>();
        return services;
    }
}
