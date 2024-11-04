﻿using Ardalis.Specification;
using TGF.Common.ROP.HttpResult;

namespace TGF.CA.Domain.Contracts.Repositories
{
    /// <summary>
    /// Provides a set of methods for executing queries and retrieving(including get by id) entities in a read only repository(CQRS friendly).
    /// </summary>
    /// <remarks>Works only with <see cref=" IEntity{TKey}"/> as entity type. Includes by Id operations.</remarks>
    public interface IEntityQueryRepository<T, TKey>
        : IQueryRepository<T>
        where T : class, IEntity<TKey>, new()
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
        /// <param name="aEntityIdList">The list IDs used to query the entities.</param>
        /// <param name="aCancellationToken">Cancellation token to cancel the query if needed.</param>
        /// <returns></returns>
        Task<IHttpResult<IEnumerable<T>>> GetByIdListAsync(IEnumerable<TKey> aEntityIdList, CancellationToken aCancellationToken = default);

        /// <summary>
        /// Retrieves the list of entities from the database.
        /// </summary>
        /// <param name="specification">The specification used to query the databse.</param>
        /// <param name="cancellationToken">Cancellation token to cancel the query if needed.</param>
        /// <returns>All entities of type <see cref="T"/> from the database.</returns>
        Task<IHttpResult<IEnumerable<T>>> GetListAsync(ISpecification<T> specification, CancellationToken cancellationToken = default);

        /// <summary>
        /// Get the count of records in the DB for the <see cref="T"/> entitiy.
        /// </summary>
        /// <param name="cancellationToken"></param>
        /// <returns></returns>
        Task<IHttpResult<int>> GetCountAsync(CancellationToken cancellationToken = default);

        #endregion

    }
}
