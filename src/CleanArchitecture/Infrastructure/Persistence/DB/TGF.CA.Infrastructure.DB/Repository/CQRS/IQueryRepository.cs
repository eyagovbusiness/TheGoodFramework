using TGF.CA.Domain.Contracts;
using TGF.Common.ROP.HttpResult;

namespace TGF.CA.Infrastructure.DB.Repository.CQRS
{
    /// <summary>
    /// Provides a set of methods for executing queries and retrieving entities in a read only repository(CQRS friendly).
    /// </summary>
    public interface IQueryRepository<T, TKey>
        where T : class, IEntity<TKey>
        where TKey : struct, IEquatable<TKey>
    {
        #region Query

        /// <summary>
        /// Attempts to execute query logic asynchronously and returns the ROP result of type <see cref="IHttpResult{T}"/>.
        /// </summary>
        /// <typeparam name="TResult">The type of the result.</typeparam>
        /// <param name="aQueryAsyncAction">The asynchronous query logic to execute.</param>
        /// <param name="aCancellationToken">Optional cancellation token to cancel the operation.</param>
        /// <returns>A task that represents the asynchronous operation, containing the result of the query execution.</returns>
        Task<IHttpResult<TResult>> TryQueryAsync<TResult>(Func<CancellationToken, Task<IHttpResult<TResult>>> aQueryAsyncAction, CancellationToken aCancellationToken = default);

        /// <summary>
        /// Attempts to execute query logic asynchronously that returns the result of type <see cref="{T}"/>.
        /// </summary>
        /// <typeparam name="TResult">The type of the result.</typeparam>
        /// <param name="aQueryAsyncAction">The asynchronous query logic to execute.</param>
        /// <param name="aCancellationToken">Optional cancellation token to cancel the operation.</param>
        /// <returns>A task that represents the asynchronous operation, containing the result of the query execution.</returns>
        Task<IHttpResult<TResult>> TryQueryAsync<TResult>(Func<CancellationToken, Task<TResult>> aQueryAsyncAction, CancellationToken aCancellationToken = default);

        /// <summary>
        /// Attempts to execute query logic and returns the ROP result of type <see cref="IHttpResult{T}"/>.
        /// </summary>
        /// <typeparam name="TResult">The type of the result.</typeparam>
        /// <param name="aQueryAction">The query logic to execute.</param>
        /// <returns>The result of the query execution.</returns>
        IHttpResult<TResult> TryQuery<TResult>(Func<IHttpResult<TResult>> aQueryAction);

        /// <summary>
        /// Attempts to execute query logic and returns the result of type <see cref="{T}"/>.
        /// </summary>
        /// <typeparam name="TResult">The type of the result.</typeparam>
        /// <param name="aQueryAction">The query logic to execute.</param>
        /// <returns>The result of the query execution.</returns>
        IHttpResult<TResult> TryQuery<TResult>(Func<TResult> aQueryAction);

        #endregion

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
        /// <param name="aEntityIdList"></param>
        /// <param name="aCancellationToken"></param>
        /// <returns></returns>
        Task<IHttpResult<IEnumerable<T>>> GetByIdListAsync(IEnumerable<TKey> aEntityIdList, CancellationToken aCancellationToken = default);

        #endregion

    }

}