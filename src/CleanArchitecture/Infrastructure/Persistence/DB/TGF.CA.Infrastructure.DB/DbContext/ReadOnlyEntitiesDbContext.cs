using Microsoft.EntityFrameworkCore;
namespace TGF.CA.Infrastructure.DB.DbContext
{
    public class ReadOnlyEntitiesDbContext<TDbContext>(DbContextOptions options)
        : EntitiesDbContext<TDbContext>(options), IReadOnlyDbContext
        where TDbContext : Microsoft.EntityFrameworkCore.DbContext
    {
        // Explicitly implement the IReadOnlyDbContext methods
        public IQueryable<TEntity> Query<TEntity>() where TEntity : class
        => Set<TEntity>().AsNoTracking(); // Ensures that EF does not track changes

        public async override ValueTask<TEntity?> FindAsync<TEntity>(params object?[]? keyValues) where TEntity : class
        => await Set<TEntity>().FindAsync(keyValues);

    }
}
