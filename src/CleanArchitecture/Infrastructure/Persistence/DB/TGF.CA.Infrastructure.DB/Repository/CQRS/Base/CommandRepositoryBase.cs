using Microsoft.EntityFrameworkCore.Storage;
using Microsoft.Extensions.Logging;
using TGF.Common.ROP;
using TGF.Common.ROP.Errors;
using TGF.Common.ROP.HttpResult;
using TGF.Common.ROP.HttpResult.RailwaySwitches;
using TGF.Common.ROP.Result;

namespace TGF.CA.Infrastructure.DB.Repository.CQRS.Base
{
    /// <summary>
    /// Base class for command repositories with the TryCommand and TrySaveChanges result abstractions.
    /// </summary>
    public class CommandRepositoryBase<TRepository, TDbContext>(TDbContext context, ILogger<TRepository> logger)
        where TDbContext : Microsoft.EntityFrameworkCore.DbContext
        where TRepository : class
    {

        #region Command
        public async Task<IHttpResult<TResult>> TryCommandAsync<TResult>(Func<CancellationToken, Task<IHttpResult<TResult>>> aCommandAsyncAction, Func<int, TResult, IHttpResult<TResult>>? aSaveResultOverride = default, CancellationToken aCancellationToken = default) {
            try {
                return await Result.CancellationTokenResult(aCancellationToken)
                    .Bind(_ => aCommandAsyncAction(aCancellationToken))
                    .Bind(commandResult => TrySaveChangesAsync(commandResult, aSaveResultOverride, aCancellationToken));
            }
            catch (Exception lEx) {
                return GetCommandExceptionResult<TResult>(lEx);
            }
        }

        public async Task<IHttpResult<TResult>> TryCommandAsync<TResult>(Func<CancellationToken, Task<TResult>> aCommandAsyncAction, Func<int, TResult, IHttpResult<TResult>>? aSaveResultOverride = default, CancellationToken aCancellationToken = default) {
            try {
                return await Result.CancellationTokenResult(aCancellationToken)
                    .Map(_ => aCommandAsyncAction(aCancellationToken))
                    .Bind(commandResult => TrySaveChangesAsync(commandResult, aSaveResultOverride, aCancellationToken));
            }
            catch (Exception lEx) {
                return GetCommandExceptionResult<TResult>(lEx);
            }
        }

        public async Task<IHttpResult<TResult>> TryCommandAsync<TResult>(Func<IHttpResult<TResult>> aCommandAction, Func<int, TResult, IHttpResult<TResult>>? aSaveResultOverride = default, CancellationToken aCancellationToken = default) {
            try {
                return await Result.CancellationTokenResult(aCancellationToken)
                    .Bind(_ => aCommandAction())
                    .Bind(commandResult => TrySaveChangesAsync(commandResult, aSaveResultOverride, aCancellationToken));
            }
            catch (Exception lEx) {
                return GetCommandExceptionResult<TResult>(lEx);
            }
        }

        public async Task<IHttpResult<TResult>> TryCommandAsync<TResult>(Func<TResult> aCommandAction, Func<int, TResult, IHttpResult<TResult>>? aSaveResultOverride = default, CancellationToken aCancellationToken = default) {
            try {
                return await Result.CancellationTokenResult(aCancellationToken)
                    .Map(_ => aCommandAction())
                    .Bind(commandResult => TrySaveChangesAsync(commandResult, aSaveResultOverride, aCancellationToken));
            }
            catch (Exception lEx) {
                return GetCommandExceptionResult<TResult>(lEx);
            }
        }

        #endregion

        #region Transactions
        public async Task<IHttpResult<IDbContextTransaction>> BeginTransactionAsync(CancellationToken aCancellationToken = default) {
            try {
                var lTransaction = await context.Database.BeginTransactionAsync(aCancellationToken);
                return Result.SuccessHttp(lTransaction);
            }
            catch (Exception lEx) {
                return GetTransactionExceptionResult<IDbContextTransaction>(lEx);
            }
        }

        public async Task<IHttpResult<Unit>> CommitTransactionAsync(IDbContextTransaction aTransaction, CancellationToken aCancellationToken = default) {
            try {
                await aTransaction.CommitAsync(aCancellationToken);
                return Result.SuccessHttp(Unit.Value);
            }
            catch (Exception lEx) {
                return GetTransactionExceptionResult<IDbContextTransaction>(lEx).Map(err => Unit.Value);
            }
        }

        public async Task<IHttpResult<Unit>> RollbackTransactionAsync(IDbContextTransaction aTransaction, CancellationToken aCancellationToken = default) {
            try {
                await aTransaction.RollbackAsync(aCancellationToken);
                return Result.SuccessHttp(Unit.Value);
            }
            catch (Exception lEx) {
                logger.LogError(lEx, "An error occurred while rolling back the DB transaction: {ErrorMessage}", lEx.Message);
                return Result.Failure<Unit>(CommonErrors.UnhandledException.New(lEx.Message));
            }
        }

        #endregion

        #region Save
        public async Task<IHttpResult<TResult>> TrySaveChangesAsync<TResult>(TResult aResult, Func<int, TResult, IHttpResult<TResult>>? aSaveResultOverride = default, CancellationToken aCancellationToken = default) {
            try {
                if (aSaveResultOverride == default)
                    return DefaultSaveResultFunc(
                        await context.SaveChangesAsync(aCancellationToken)
                        , aResult);

                return aSaveResultOverride(
                    await context.SaveChangesAsync(aCancellationToken)
                    , aResult);
            }
            catch (Exception lEx) {
                logger.LogError(lEx, "An error occurred while saving DB changes in {TRepository} for entity {EntityName}: {ErrorMessage}", nameof(TRepository), nameof(TResult), lEx.Message);
                return Result.Failure<TResult>(CommonErrors.UnhandledException.New(lEx.Message));
            }
        }
        public virtual IHttpResult<TResult> DefaultSaveResultFunc<TResult>(int aChangeCount, TResult aCommandResult)
        => !context.ChangeTracker.HasChanges() || aChangeCount > 0
            ? Result.SuccessHttp(aCommandResult)
            : Result.Failure<TResult>(DBErrors.Repository.Save.Error);

        #endregion

        #region Private
        private IHttpResult<TResult> GetCommandExceptionResult<TResult>(Exception exception) {
            const string errorMessageTemplate = "An error occurred trying to execute a DB command in {TRepository} for entity {EntityName}: {ExceptionMessage}";
            logger.LogError(exception, errorMessageTemplate, nameof(TRepository), nameof(TResult), exception.Message);

            var errorMessage = $"An error occurred trying to execute a DB command in {nameof(TRepository)} for entity {nameof(TResult)}: {exception.Message}";
            return Result.Failure<TResult>(CommonErrors.UnhandledException.New(errorMessage));
        }
        private IHttpResult<IDbContextTransaction> GetTransactionExceptionResult<IDbContextTransaction>(Exception exception) {
            const string errorMessageTemplate = "An error occurred while commiting the DB transaction in {TRepository}: {ErrorMessage}";
            logger.LogError(exception, errorMessageTemplate, nameof(TRepository), exception.Message);

            var errorMessage = $"An error occurred while commiting the DB transaction in {nameof(TRepository)}: {exception.Message}";
            return Result.Failure<IDbContextTransaction>(CommonErrors.UnhandledException.New(errorMessage));
        }
        #endregion

    }
}
