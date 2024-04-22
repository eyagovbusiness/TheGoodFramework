using TGF.CA.Domain.Contracts;
using TGF.Common.ROP.HttpResult;

namespace TGF.CA.Infrastructure.DB.Repository.CQRS
{
    /// <summary>
    /// Provides a set of methods for executing queries and retrieving entities in a read only repository(CQRS friendly).
    /// </summary>
    public interface IQueryRepository
    {
        /// <summary>
        /// Attempts to execute query logic asynchronously and returns the ROP result of type <see cref="IHttpResult{T}"/>.
        /// </summary>
        /// <typeparam name="T">The type of the result.</typeparam>
        /// <param name="aQueryAsyncAction">The asynchronous query logic to execute.</param>
        /// <param name="aCancellationToken">Optional cancellation token to cancel the operation.</param>
        /// <returns>A task that represents the asynchronous operation, containing the result of the query execution.</returns>
        Task<IHttpResult<T>> TryQueryAsync<T>(Func<CancellationToken, Task<IHttpResult<T>>> aQueryAsyncAction, CancellationToken aCancellationToken = default);

        /// <summary>
        /// Attempts to execute query logic asynchronously that returns the result of type <see cref="{T}"/>.
        /// </summary>
        /// <typeparam name="T">The type of the result.</typeparam>
        /// <param name="aQueryAsyncAction">The asynchronous query logic to execute.</param>
        /// <param name="aCancellationToken">Optional cancellation token to cancel the operation.</param>
        /// <returns>A task that represents the asynchronous operation, containing the result of the query execution.</returns>
        Task<IHttpResult<T>> TryQueryAsync<T>(Func<CancellationToken, Task<T>> aQueryAsyncAction, CancellationToken aCancellationToken = default);

        /// <summary>
        /// Attempts to execute query logic and returns the ROP result of type <see cref="IHttpResult{T}"/>.
        /// </summary>
        /// <typeparam name="T">The type of the result.</typeparam>
        /// <param name="aQueryAction">The query logic to execute.</param>
        /// <returns>The result of the query execution.</returns>
        IHttpResult<T> TryQuery<T>(Func<IHttpResult<T>> aQueryAction);

        /// <summary>
        /// Attempts to execute query logic and returns the result of type <see cref="{T}"/>.
        /// </summary>
        /// <typeparam name="T">The type of the result.</typeparam>
        /// <param name="aQueryAction">The query logic to execute.</param>
        /// <returns>The result of the query execution.</returns>
        IHttpResult<T> TryQuery<T>(Func<T> aQueryAction);

        /// <summary>
        /// Retrieves an entity by its identifier asynchronously.
        /// </summary>
        /// <typeparam name="T">The type of the entity.</typeparam>
        /// <param name="aEntityId">The identifier of the entity.</param>
        /// <param name="aCancellationToken">Optional cancellation token to cancel the operation.</param>
        /// <returns>A task that represents the asynchronous operation, containing the result of retrieving the entity.</returns>
        Task<IHttpResult<T>> GetByIdAsync<T, TKey>(TKey aEntityId, CancellationToken aCancellationToken = default)
            where T : class
            where TKey : struct, IEquatable<TKey>;

        /// <summary>
        /// Retrieves a list of entities by its identifiers asynchronously.
        /// </summary>
        /// <typeparam name="T"></typeparam>
        /// <typeparam name="TKey"></typeparam>
        /// <param name="aEntityIdList"></param>
        /// <param name="aCancellationToken"></param>
        /// <returns></returns>
        Task<IHttpResult<List<T>>> GetByIdListAsync<T, TKey>(IEnumerable<TKey> aEntityIdList, CancellationToken aCancellationToken = default)
            where T : class, IEntity<TKey>
            where TKey : struct, IEquatable<TKey>;

    }

}