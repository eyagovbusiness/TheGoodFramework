
namespace TGF.CA.Infrastructure.DB.DbContext
{
    public interface IReadOnlyDbContext
    {
        IQueryable<TEntity> Query<TEntity>() where TEntity : class;
        ValueTask<TEntity?> FindAsync<TEntity>(params object?[]? keyValues) where TEntity : class;
    }
}
