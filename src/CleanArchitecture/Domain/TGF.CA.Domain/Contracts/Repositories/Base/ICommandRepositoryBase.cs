using TGF.Common.ROP.HttpResult;

namespace TGF.CA.Domain.Contracts.Repositories.Base
{
    /// <summary>
    /// Interface for command repositorties working with any class as entities which defines the TryCommand methods.
    /// </summary>
    public interface ICommandRepositoryBase<T>
       where T : class
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

        #region Save
        /// <summary>
        /// Attempts to save changes to the repository asynchronously handling exceptions or errors and returning them as ROP Errors.
        /// </summary>
        /// <typeparam name="TResult">The type of the result.</typeparam>
        /// <param name="aResult">The result to attempt to save.</param>
        /// <param name="aCancellationToken">Optional cancellation token to cancel the operation.</param>
        /// <param name="aSaveResultOverride">Optional function to override the default save result handling.</param>
        /// <returns>A task that represents the asynchronous save operation, containing the result of the save operation.</returns>
        Task<IHttpResult<TResult>> TrySaveChangesAsync<TResult>(TResult aResult, Func<int, TResult, IHttpResult<TResult>>? aSaveResultOverride = default, CancellationToken aCancellationToken = default);

        /// <summary>
        /// Provides the default save result function which maps the repository save changes result into a ROP result with the value of type <see cref="{T}"/> resulting from the command logic. 
        /// </summary>
        /// <typeparam name="TResult">The type of the command result.</typeparam>
        /// <param name="aChangeCount">The number of changes saved returned by the SaveChangesAsync DbContext function.</param>
        /// <param name="aCommandResult">The command result.</param>
        /// <returns>The ROP result after the save operation.</returns>
        IHttpResult<TResult> DefaultSaveResultFunc<TResult>(int aChangeCount, TResult aCommandResult);

        #endregion

    }
}
