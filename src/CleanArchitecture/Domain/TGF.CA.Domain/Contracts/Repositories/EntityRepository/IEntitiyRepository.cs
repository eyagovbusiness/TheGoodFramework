
namespace TGF.CA.Domain.Contracts.Repositories.EntityRepository
{
    /// <summary>
    /// Interface for repositories working with <see cref="IEntity{TKey}"/> as entitiy type with the base default CRUD implementations.
    /// </summary>
    public interface IEntitiyRepository<T, TKey> 
        : IEntityCommandRepository<T, TKey>, IEntityQueryRepository<T, TKey>
        where T : class, IEntity<TKey>
        where TKey : struct, IEquatable<TKey>
    {

    }
}
