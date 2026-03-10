using Aggregator.Core.Entities;
using Microsoft.EntityFrameworkCore;

namespace Aggregator.Data.Sqlite;

public class NewsContext(DbContextOptions<NewsContext> options) : DbContext(options)
{
    public DbSet<NewsItem> NewsItems => Set<NewsItem>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<NewsItem>(entity =>
        {
            entity.HasKey(e => e.Id);
            entity.HasIndex(e => new { e.Url, e.Source }).IsUnique();
            entity.Property(e => e.Title).IsRequired();
            entity.Property(e => e.Url).IsRequired();
            entity.Property(e => e.Source).IsRequired();
        });
    }
}
