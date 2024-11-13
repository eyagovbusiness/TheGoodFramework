using TGF.Common.ROP.HttpResult;

namespace TGF.CA.Domain.Contracts.Repositories.EntityRepository
{
    /// <summary>
    /// Provides a set of methods for executing queries and retrieving entities in a read only repository(CQRS friendly).
    /// </summary>
    public interface IEntityQueryRepository<T, TKey>
        : IQueryRepository<T>
        where T : class, IEntity<TKey>
        where TKey : IEquatable<TKey>
    {

        #region Read

        /// <summary>
        /// Retrieves an entity by its identifier asynchronously.
        /// </summary>
        /// <typeparam name="T">The type of the entity.</typeparam>
        /// <param name="aEntityId">The identifier of the entity.</param>
        /// <param name="aCancellationToken">Optional cancellation token to cancel the operation.</param>
        /// <returns>A task that represents the asynchronous operation, containing the result of retrieving the entity.</returns>
        Task<IHttpResult<T>> GetByIdAsync(TKey aEntityId, CancellationToken aCancellationToken = default);

        /// <summary>
        /// Retrieves a list of entities by its identifiers asynchronously.
        /// </summary>
        /// <param name="aEntityIdList">The list IDs used to query the entities.</param>
        /// <param name="aCancellationToken">Cancellation token to cancel the query if needed.</param>
        /// <returns></returns>
        Task<IHttpResult<IEnumerable<T>>> GetByIdListAsync(IEnumerable<TKey> aEntityIdList, CancellationToken aCancellationToken = default);

        #endregion

    }
}
