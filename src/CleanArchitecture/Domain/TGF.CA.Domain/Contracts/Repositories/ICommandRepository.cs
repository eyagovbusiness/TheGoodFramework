using TGF.Common.ROP.HttpResult;

namespace TGF.CA.Domain.Contracts.Repositories
{
    /// <summary>
    /// Provides a set of methods for executing commands in a write repository, handling CRUD operations, managing transactions, and saving changes(CQRS friendly).
    /// </summary>
    public interface ICommandRepository<T, TKey>
       where T : class, IEntity<TKey>
       where TKey : struct, IEquatable<TKey>
    {
        #region Command
        /// <summary>
        /// Attempts to execute command logic asynchronously and returns the command ROP result of type <see cref="IHttpResult{T}"/>.
        /// </summary>
        /// <typeparam name="TResult">The type of the result.</typeparam>
        /// <param name="aCommandAsyncAction">The asynchronous command logic to execute.</param>
        /// <param name="aCancellationToken">Optional cancellation token to cancel the operation.</param>
        /// <param name="aSaveResultOverride">Optional function to override the default save result handling.</param>
        /// <returns>A task that represents the asynchronous operation, containing the result of the command execution.</returns>
        Task<IHttpResult<TResult>> TryCommandAsync<TResult>(Func<CancellationToken, Task<IHttpResult<TResult>>> aCommandAsyncAction, Func<int, TResult, IHttpResult<TResult>> aSaveResultOverride = default!, CancellationToken aCancellationToken = default);
        /// <summary>
        /// Attempts to execute command logic asynchronously that returns the command result of type <see cref="{T}"/>.
        /// </summary>
        /// <typeparam name="TResult">The type of the result.</typeparam>
        /// <param name="aCommandAsyncAction">The asynchronous command logic to execute.</param>
        /// <param name="aCancellationToken">Optional cancellation token to cancel the operation.</param>
        /// <param name="aSaveResultOverride">Optional function to override the default save result handling.</param>
        /// <returns>A task that represents the asynchronous operation, containing the result of the command execution.</returns>
        Task<IHttpResult<TResult>> TryCommandAsync<TResult>(Func<CancellationToken, Task<TResult>> aCommandAsyncAction, Func<int, TResult, IHttpResult<TResult>> aSaveResultOverride = default!, CancellationToken aCancellationToken = default);
        /// <summary>
        /// Attempts to execute command logic and returns the the command ROP result of type <see cref="IHttpResult{T}"/>.
        /// </summary>
        /// <typeparam name="TResult">The type of the result.</typeparam>
        /// <param name="aCommandAction">The command logic to execute.</param>
        /// <param name="aCancellationToken">Optional cancellation token to cancel the operation.</param>
        /// <param name="aSaveResultOverride">Optional function to override the default save result handling.</param>
        /// <returns>A task that represents the asynchronous operation, containing the result of the command execution.</returns>
        Task<IHttpResult<TResult>> TryCommandAsync<TResult>(Func<IHttpResult<TResult>> aCommandAction, Func<int, TResult, IHttpResult<TResult>> aSaveResultOverride = default!, CancellationToken aCancellationToken = default);
        /// <summary>
        /// Attempts to execute command logic and returns the command result of type <see cref="{T}"/>.
        /// </summary>
        /// <typeparam name="TResult">The type of the result.</typeparam>
        /// <param name="aCommandAction">The command logic to execute.</param>
        /// <param name="aCancellationToken">Optional cancellation token to cancel the operation.</param>
        /// <param name="aSaveResultOverride">Optional function to override the default save result handling.</param>
        /// <returns>A task that represents the asynchronous operation, containing the result of the command execution.</returns>
        Task<IHttpResult<TResult>> TryCommandAsync<TResult>(Func<TResult> aCommandAction, Func<int, TResult, IHttpResult<TResult>> aSaveResultOverride = default!, CancellationToken aCancellationToken = default);
        #endregion

        #region Create-Update-Delete
        /// <summary>
        /// Asynchronously adds an entity to the repository.
        /// </summary>
        Task<IHttpResult<T>> AddAsync(T aEntity, CancellationToken aCancellationToken = default);
        /// <summary>
        /// Asynchronously updates an entity in the repository.
        /// </summary>
        Task<IHttpResult<T>> UpdateAsync(T aEntity, CancellationToken aCancellationToken = default);
        /// <summary>
        /// Asynchronously deletes an entity from the repository.
        /// </summary>
        Task<IHttpResult<T>> DeleteAsync(T aEntity, CancellationToken aCancellationToken = default);
        #endregion

    }
}
