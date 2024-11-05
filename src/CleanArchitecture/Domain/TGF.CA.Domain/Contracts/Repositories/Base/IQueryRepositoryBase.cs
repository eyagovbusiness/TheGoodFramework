using TGF.Common.ROP.HttpResult;

namespace TGF.CA.Domain.Contracts.Repositories.Base
{
    /// <summary>
    /// Interface for query repositorties working with any class as entities which defines the TryQuery methods.
    /// </summary>
    public interface IQueryRepositoryBase<T>
        where T : class
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

    }
}
