using TGF.CA.Domain.Contracts;
using TGF.CA.Domain.Contracts.Repositories;

namespace TGF.CA.Infrastructure.DB.Repository.CQRS.Internal
{
    /// <summary>
    /// Provides a set of methods for executing queries and retrieving entities in a read only repository(CQRS friendly).
    /// </summary>
    internal interface IQueryRepositoryInternal<T, TKey> : IQueryRepository<T, TKey>
        where T : class, IEntity<TKey>
        where TKey : struct, IEquatable<TKey>
    {

    }

}