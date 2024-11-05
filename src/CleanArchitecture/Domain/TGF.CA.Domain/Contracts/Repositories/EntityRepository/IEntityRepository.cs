
namespace TGF.CA.Domain.Contracts.Repositories.EntityRepository
{
    /// <summary>
    /// Interface for repositories working with <see cref="IEntity{TKey}"/> as entity type with the base default CRUD implementations.
    /// </summary>
    public interface IEntityRepository<T, TKey> 
        : IEntityCommandRepository<T, TKey>, IEntityQueryRepository<T, TKey>
        where T : class, IEntity<TKey>
        where TKey : struct, IEquatable<TKey>
    {

    }
}
