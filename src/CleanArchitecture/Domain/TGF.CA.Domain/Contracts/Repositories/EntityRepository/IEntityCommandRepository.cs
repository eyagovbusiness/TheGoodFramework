namespace TGF.CA.Domain.Contracts.Repositories.EntityRepository
{
    /// <summary>
    /// Interface for command repositorties working with <see cref="IEntity{TKey}"/>
    /// </summary>
    public interface IEntityCommandRepository<T, TKey>
    : ICommandRepository<T>
       where T : class, IEntity<TKey>
       where TKey : struct, IEquatable<TKey>
    {

    }
}
