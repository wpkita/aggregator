using Aggregator.Core.Infrastructure;
using Microsoft.EntityFrameworkCore;

namespace Aggregator.Data.Sqlite;

public class SqliteRepository<T>(NewsContext context) : IRepository<T> where T : class
{
    public Task<T?> GetByIdAsync(int id, CancellationToken cancellationToken = default)
        => context.Set<T>().FindAsync([id], cancellationToken).AsTask();

    public async Task<IEnumerable<T>> GetAllAsync(CancellationToken cancellationToken = default)
        => await context.Set<T>().ToListAsync(cancellationToken);

    public async Task AddAsync(T entity, CancellationToken cancellationToken = default)
        => await context.Set<T>().AddAsync(entity, cancellationToken);

    public Task UpdateAsync(T entity, CancellationToken cancellationToken = default)
    {
        context.Set<T>().Update(entity);
        return Task.CompletedTask;
    }

    public Task DeleteAsync(T entity, CancellationToken cancellationToken = default)
    {
        context.Set<T>().Remove(entity);
        return Task.CompletedTask;
    }

    public Task SaveChangesAsync(CancellationToken cancellationToken = default)
        => context.SaveChangesAsync(cancellationToken);
}
