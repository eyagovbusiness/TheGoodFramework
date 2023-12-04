using Microsoft.EntityFrameworkCore.Storage;
using TGF.Common.ROP;
using TGF.Common.ROP.HttpResult;
#pragma warning disable CA1068 // CancellationToken parameters must come last
namespace TGF.CA.Infrastructure.DB.Repository.CQRS
{
    /// <summary>
    /// Provides a set of methods for executing commands in a write repository, handling CRUD operations, managing transactions, and saving changes(CQRS friendly).
    /// </summary>
    public interface ICommandRepository
    {
        #region Command
        /// <summary>
        /// Attempts to execute command logic asynchronously and returns the command ROP result of type <see cref="IHttpResult{T}"/>.
        /// </summary>
        /// <typeparam name="T">The type of the result.</typeparam>
        /// <param name="aCommandAsyncAction">The asynchronous command logic to execute.</param>
        /// <param name="aCancellationToken">Optional cancellation token to cancel the operation.</param>
        /// <param name="aSaveResultOverride">Optional function to override the default save result handling.</param>
        /// <returns>A task that represents the asynchronous operation, containing the result of the command execution.</returns>
        Task<IHttpResult<T>> TryCommandAsync<T>(Func<CancellationToken, Task<IHttpResult<T>>> aCommandAsyncAction, CancellationToken aCancellationToken = default, Func<int, T, IHttpResult<T>>? aSaveResultOverride = default);

        /// <summary>
        /// Attempts to execute command logic asynchronously that returns the command result of type <see cref="{T}"/>.
        /// </summary>
        /// <typeparam name="T">The type of the result.</typeparam>
        /// <param name="aCommandAsyncAction">The asynchronous command logic to execute.</param>
        /// <param name="aCancellationToken">Optional cancellation token to cancel the operation.</param>
        /// <param name="aSaveResultOverride">Optional function to override the default save result handling.</param>
        /// <returns>A task that represents the asynchronous operation, containing the result of the command execution.</returns>
        Task<IHttpResult<T>> TryCommandAsync<T>(Func<CancellationToken, Task<T>> aCommandAsyncAction, CancellationToken aCancellationToken = default, Func<int, T, IHttpResult<T>>? aSaveResultOverride = default);

        /// <summary>
        /// Attempts to execute command logic and returns the the command ROP result of type <see cref="IHttpResult{T}"/>.
        /// </summary>
        /// <typeparam name="T">The type of the result.</typeparam>
        /// <param name="aCommandAction">The command logic to execute.</param>
        /// <param name="aCancellationToken">Optional cancellation token to cancel the operation.</param>
        /// <param name="aSaveResultOverride">Optional function to override the default save result handling.</param>
        /// <returns>A task that represents the asynchronous operation, containing the result of the command execution.</returns>
        Task<IHttpResult<T>> TryCommandAsync<T>(Func<IHttpResult<T>> aCommandAction, CancellationToken aCancellationToken = default, Func<int, T, IHttpResult<T>>? aSaveResultOverride = default);

        /// <summary>
        /// Attempts to execute command logic and returns the command result of type <see cref="{T}"/>.
        /// </summary>
        /// <typeparam name="T">The type of the result.</typeparam>
        /// <param name="aCommandAction">The command logic to execute.</param>
        /// <param name="aCancellationToken">Optional cancellation token to cancel the operation.</param>
        /// <param name="aSaveResultOverride">Optional function to override the default save result handling.</param>
        /// <returns>A task that represents the asynchronous operation, containing the result of the command execution.</returns>
        Task<IHttpResult<T>> TryCommandAsync<T>(Func<T> aCommandAction, CancellationToken aCancellationToken = default, Func<int, T, IHttpResult<T>>? aSaveResultOverride = default);

        #endregion

        #region Create-Update-Delete
        /// <summary>
        /// Asynchronously adds an entity to the repository.
        /// </summary>
        /// <typeparam name="T">The type of the entity to add.</typeparam>
        Task<IHttpResult<T>> AddAsync<T>(T aEntity, CancellationToken aCancellationToken = default) where T : class;

        /// <summary>
        /// Asynchronously updates an entity in the repository.
        /// </summary>
        /// <typeparam name="T">The type of the entity to update.</typeparam>
        Task<IHttpResult<T>> UpdateAsync<T>(T aEntity, CancellationToken aCancellationToken = default) where T : class;

        /// <summary>
        /// Asynchronously deletes an entity from the repository.
        /// </summary>
        /// <typeparam name="T">The type of the entity to delete.</typeparam>
        Task<IHttpResult<T>> DeleteAsync<T>(T aEntity, CancellationToken aCancellationToken = default) where T : class;

        #endregion

        #region Transactions
        /// <summary>
        /// Begins a transaction asynchronously.
        /// </summary>
        Task<IHttpResult<IDbContextTransaction>> BeginTransactionAsync(CancellationToken aCancellationToken = default);

        /// <summary>
        /// Commits a transaction asynchronously.
        /// </summary>
        Task<IHttpResult<Unit>> CommitTransactionAsync(IDbContextTransaction aTransaction, CancellationToken aCancellationToken = default);

        /// <summary>
        /// Rolls back a transaction asynchronously.
        /// </summary>
        Task<IHttpResult<Unit>> RollbackTransactionAsync(IDbContextTransaction aTransaction, CancellationToken aCancellationToken = default);

        #endregion

        #region Save
        /// <summary>
        /// Attempts to save changes to the repository asynchronously handling exceptions or errors and returning them as ROP Errors.
        /// </summary>
        /// <typeparam name="T">The type of the result.</typeparam>
        /// <param name="aResult">The result to attempt to save.</param>
        /// <param name="aCancellationToken">Optional cancellation token to cancel the operation.</param>
        /// <param name="aSaveResultOverride">Optional function to override the default save result handling.</param>
        /// <returns>A task that represents the asynchronous save operation, containing the result of the save operation.</returns>
        Task<IHttpResult<T>> TrySaveChangesAsync<T>(T aResult, CancellationToken aCancellationToken = default, Func<int, T, IHttpResult<T>>? aSaveResultOverride = default);

        /// <summary>
        /// Provides the default save result function which maps the repository save changes result into a ROP result with the value of type <see cref="{T}"/> resulting from the command logic. 
        /// </summary>
        /// <typeparam name="T">The type of the command result.</typeparam>
        /// <param name="aChangeCount">The number of changes saved returned by the SaveChangesAsync DbContext function.</param>
        /// <param name="aCommandResult">The command result.</param>
        /// <returns>The ROP result after the save operation.</returns>
        IHttpResult<T> DefaultSaveResultFunc<T>(int aChangeCount, T aCommandResult);

        #endregion

    }

}