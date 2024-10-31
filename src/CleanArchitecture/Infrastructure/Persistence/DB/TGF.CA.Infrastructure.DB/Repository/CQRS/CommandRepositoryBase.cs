using Microsoft.EntityFrameworkCore.Storage;
using Microsoft.Extensions.Logging;
using TGF.CA.Domain.Contracts;
using TGF.CA.Infrastructure.DB.Repository.CQRS.Internal;
using TGF.Common.ROP;
using TGF.Common.ROP.Errors;
using TGF.Common.ROP.HttpResult;
using TGF.Common.ROP.HttpResult.RailwaySwitches;
using TGF.Common.ROP.Result;

namespace TGF.CA.Infrastructure.DB.Repository.CQRS {
    /// <summary>
    /// A base class for a CQRS write repository with native error handling logic for Command operations using ROP.
    /// </summary>
    /// <typeparam name="TRepository">The type of the child class implementing this repository.</typeparam>
    /// <typeparam name="TDbContext">The type of the DbContext to use in this repository.</typeparam>
    public abstract class CommandRepositoryBase<TRepository, TDbContext, T, TKey>(TDbContext aContext, ILogger<TRepository> aLogger) : ICommandRepositoryInternal<T, TKey>
        where TDbContext : Microsoft.EntityFrameworkCore.DbContext
        where TRepository : class
        where T : class, IEntity<TKey>
         where TKey : struct, IEquatable<TKey>
    {
        protected readonly TDbContext context = aContext;
        protected readonly ILogger<TRepository> logger = aLogger;

        #region ICommandRepository

        #region Command
        public async Task<IHttpResult<TResult>> TryCommandAsync<TResult>(Func<CancellationToken, Task<IHttpResult<TResult>>> aCommandAsyncAction, CancellationToken aCancellationToken = default, Func<int, TResult, IHttpResult<TResult>>? aSaveResultOverride = default)
        {
            try
            {
                return await Result.CancellationTokenResult(aCancellationToken)
                    .Bind(_ => aCommandAsyncAction(aCancellationToken))
                    .Bind(commandResult => TrySaveChangesAsync(commandResult, aCancellationToken, aSaveResultOverride));
            }
            catch (Exception lEx)
            {
                logger.LogError(lEx, "An error occurred trying to execute a DB command at repository level : {ErrorMessage}", lEx.Message);
                return Result.Failure<TResult>(CommonErrors.UnhandledException.New(lEx.Message));
            }
        }

        public async Task<IHttpResult<TResult>> TryCommandAsync<TResult>(Func<CancellationToken, Task<TResult>> aCommandAsyncAction, CancellationToken aCancellationToken = default, Func<int, TResult, IHttpResult<TResult>>? aSaveResultOverride = default)
        {
            try
            {
                return await Result.CancellationTokenResult(aCancellationToken)
                    .Map(_ => aCommandAsyncAction(aCancellationToken))
                    .Bind(commandResult => TrySaveChangesAsync(commandResult, aCancellationToken, aSaveResultOverride));
            }
            catch (Exception lEx)
            {
                logger.LogError(lEx, "An error occurred trying to execute a DB command at repository level : {ErrorMessage}", lEx.Message);
                return Result.Failure<TResult>(CommonErrors.UnhandledException.New(lEx.Message));
            }
        }

        public async Task<IHttpResult<TResult>> TryCommandAsync<TResult>(Func<IHttpResult<TResult>> aCommandAction, CancellationToken aCancellationToken = default, Func<int, TResult, IHttpResult<TResult>>? aSaveResultOverride = default)
        {
            try
            {
                return await Result.CancellationTokenResult(aCancellationToken)
                    .Bind(_ => aCommandAction())
                    .Bind(commandResult => TrySaveChangesAsync(commandResult, aCancellationToken, aSaveResultOverride));
            }
            catch (Exception lEx)
            {
                logger.LogError(lEx, "An error occurred trying to execute a DB command at repository level : {ErrorMessage}", lEx.Message);
                return Result.Failure<TResult>(CommonErrors.UnhandledException.New(lEx.Message));
            }
        }

        public async Task<IHttpResult<TResult>> TryCommandAsync<TResult>(Func<TResult> aCommandAction, CancellationToken aCancellationToken = default, Func<int, TResult, IHttpResult<TResult>>? aSaveResultOverride = default)
        {
            try
            {
                return await Result.CancellationTokenResult(aCancellationToken)
                    .Map(_ => aCommandAction())
                    .Bind(commandResult => TrySaveChangesAsync(commandResult, aCancellationToken, aSaveResultOverride));
            }
            catch (Exception lEx)
            {
                logger.LogError(lEx, "An error occurred trying to execute a DB command at repository level : {ErrorMessage}", lEx.Message);
                return Result.Failure<TResult>(CommonErrors.UnhandledException.New(lEx.Message));
            }
        }

        #endregion

        #region Create-Update-Delete
        public virtual async Task<IHttpResult<T>> AddAsync(T aEntity, CancellationToken aCancellationToken = default)
        => await TryCommandAsync(async (aCancellationToken) => (await context.Set<T>().AddAsync(aEntity, aCancellationToken)).Entity, aCancellationToken);

        public virtual async Task<IHttpResult<T>> UpdateAsync(T aEntity, CancellationToken aCancellationToken = default)
        => await TryCommandAsync(() => context.Set<T>().Update(aEntity).Entity, aCancellationToken);

        public virtual async Task<IHttpResult<T>> DeleteAsync(T aEntity, CancellationToken aCancellationToken = default)
        => await TryCommandAsync(() => context.Set<T>().Remove(aEntity).Entity, aCancellationToken);

        #endregion

        #region Transactions
        public async Task<IHttpResult<IDbContextTransaction>> BeginTransactionAsync(CancellationToken aCancellationToken = default)
        {
            try
            {
                var lTransaction = await context.Database.BeginTransactionAsync(aCancellationToken);
                return Result.SuccessHttp(lTransaction);
            }
            catch (Exception lEx)
            {
                logger.LogError(lEx, "An error occurred while beggining back the DB transaction: {ErrorMessage}", lEx.Message);
                return Result.Failure<IDbContextTransaction>(CommonErrors.UnhandledException.New(lEx.Message));
            }
        }

        public async Task<IHttpResult<Unit>> CommitTransactionAsync(IDbContextTransaction aTransaction, CancellationToken aCancellationToken = default)
        {
            try
            {
                await aTransaction.CommitAsync(aCancellationToken);
                return Result.SuccessHttp(Unit.Value);
            }
            catch (Exception lEx)
            {
                logger.LogError(lEx, "An error occurred while commiting the DB transaction: {ErrorMessage}", lEx.Message);
                return Result.Failure<Unit>(CommonErrors.UnhandledException.New(lEx.Message));
            }
        }

        public async Task<IHttpResult<Unit>> RollbackTransactionAsync(IDbContextTransaction aTransaction, CancellationToken aCancellationToken = default)
        {
            try
            {
                await aTransaction.RollbackAsync(aCancellationToken);
                return Result.SuccessHttp(Unit.Value);
            }
            catch (Exception lEx)
            {
                logger.LogError(lEx, "An error occurred while rolling back the DB transaction: {ErrorMessage}", lEx.Message);
                return Result.Failure<Unit>(CommonErrors.UnhandledException.New(lEx.Message));
            }
        }

        #endregion

        #region Save
        public async Task<IHttpResult<TResult>> TrySaveChangesAsync<TResult>(TResult aResult, CancellationToken aCancellationToken = default, Func<int, TResult, IHttpResult<TResult>>? aSaveResultOverride = default)
        {
            try
            {
                if (aSaveResultOverride == default)
                    return DefaultSaveResultFunc(
                        await context.SaveChangesAsync(aCancellationToken)
                        , aResult);

                return aSaveResultOverride(
                    await context.SaveChangesAsync(aCancellationToken)
                    , aResult);
            }
            catch (Exception lEx)
            {
                logger.LogError(lEx, "An error occurred while saving DB changes: {ErrorMessage}", lEx.Message);
                return Result.Failure<TResult>(CommonErrors.UnhandledException.New(lEx.Message));
            }
        }
        public virtual IHttpResult<TResult> DefaultSaveResultFunc<TResult>(int aChangeCount, TResult aCommandResult)
        => !context.ChangeTracker.HasChanges() || aChangeCount > 0
            ? Result.SuccessHttp(aCommandResult)
            : Result.Failure<TResult>(DBErrors.Repository.Save.Error);

        #endregion

        #endregion

    }
}
