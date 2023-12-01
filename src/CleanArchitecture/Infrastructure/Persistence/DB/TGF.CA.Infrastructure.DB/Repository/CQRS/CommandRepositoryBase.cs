using Microsoft.EntityFrameworkCore.Storage;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using TGF.Common.ROP.Errors;
using TGF.Common.ROP.HttpResult;
using TGF.Common.ROP;
using TGF.Common.ROP.Result;

namespace TGF.CA.Infrastructure.DB.Repository.CQRS
{
    public abstract class CommandRepositoryBase<TDbContext> : ICommandRepository 
    where TDbContext : DbContext
    {
        protected readonly TDbContext _context;
        protected readonly ILogger<RepositoryBase<TDbContext>> _logger;

        public CommandRepositoryBase(TDbContext aContext, ILogger<RepositoryBase<TDbContext>> aLogger)
        {
            _context = aContext;
            _logger = aLogger;
        }

        #region ICommandRepository

        #region Command
        public async Task<IHttpResult<T>> TryCommandAsync<T>(Func<CancellationToken, Task<T>> aCommandAsyncAction, CancellationToken aCancellationToken = default)
        {
            try
            {
                T lCommandAsyncActionResult = default!;
                return await Result.CancellationTokenResult(aCancellationToken)
                    .Tap(async _ => lCommandAsyncActionResult = await aCommandAsyncAction(aCancellationToken))
                    .Bind(_ => SaveChangesAsync(aCancellationToken))
                    .Map(_ => lCommandAsyncActionResult);
            }
            catch (Exception lEx)
            {
                _logger.LogError(lEx, "An error occurred trying to execute a DB command at repository level : {ErrorMessage}", lEx.Message);
                return Result.Failure<T>(CommonErrors.UnhandledException.New(lEx.Message));
            }
        }

        public async Task<IHttpResult<T>> TryCommandAsync<T>(Func<T> aCommandAction, CancellationToken aCancellationToken = default)
        {
            try
            {
                T lCommandActionResult = default!;
                return await Result.CancellationTokenResult(aCancellationToken)
                    .Tap(_ => lCommandActionResult = aCommandAction())
                    .Bind(_ => SaveChangesAsync(aCancellationToken))
                    .Map(_ => lCommandActionResult);
            }
            catch (Exception lEx)
            {
                _logger.LogError(lEx, "An error occurred trying to execute a DB command at repository level : {ErrorMessage}", lEx.Message);
                return Result.Failure<T>(CommonErrors.UnhandledException.New(lEx.Message));
            }
        }

        #endregion

        #region Create-Update-Delete
        public virtual async Task<IHttpResult<T>> AddAsync<T>(T aEntity, CancellationToken aCancellationToken = default)
            where T : class
        {
            try
            {
                await _context.Set<T>().AddAsync(aEntity, aCancellationToken);
                await _context.SaveChangesAsync(aCancellationToken);
                return Result.SuccessHttp(aEntity);
            }
            catch (Exception lEx)
            {
                _logger.LogError(lEx, "An error occurred while adding the entity to the DB: {ErrorMessage}", lEx.Message);
                return Result.Failure<T>(CommonErrors.UnhandledException.New(lEx.Message));
            }
        }

        public virtual async Task<IHttpResult<Unit>> UpdateAsync<T>(T aEntity, CancellationToken aCancellationToken = default)
            where T : class
        {
            try
            {
                _context.Set<T>().Update(aEntity);
                await _context.SaveChangesAsync(aCancellationToken);
                return Result.SuccessHttp(Unit.Value);
            }
            catch (Exception lEx)
            {
                _logger.LogError(lEx, "An error occurred while updating the DB entity: {ErrorMessage}", lEx.Message);
                return Result.Failure<Unit>(CommonErrors.UnhandledException.New(lEx.Message));
            }
        }

        public virtual async Task<IHttpResult<Unit>> DeleteAsync<T>(T aEntity, CancellationToken aCancellationToken = default)
            where T : class
        {
            try
            {
                _context.Set<T>().Remove(aEntity);
                await _context.SaveChangesAsync(aCancellationToken);
                return Result.SuccessHttp(Unit.Value);
            }
            catch (Exception lEx)
            {
                _logger.LogError(lEx, "An error occurred while deleting the entity from DB: {ErrorMessage}", lEx.Message);
                return Result.Failure<Unit>(CommonErrors.UnhandledException.New(lEx.Message));
            }
        }

        #endregion

        #region Transactions
        public async Task<IHttpResult<IDbContextTransaction>> BeginTransactionAsync(CancellationToken aCancellationToken = default)
        {
            try
            {
                var lTransaction = await _context.Database.BeginTransactionAsync(aCancellationToken);
                return Result.SuccessHttp(lTransaction);
            }
            catch (Exception lEx)
            {
                _logger.LogError(lEx, "An error occurred while beggining back the DB transaction: {ErrorMessage}", lEx.Message);
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
                _logger.LogError(lEx, "An error occurred while commiting the DB transaction: {ErrorMessage}", lEx.Message);
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
                _logger.LogError(lEx, "An error occurred while rolling back the DB transaction: {ErrorMessage}", lEx.Message);
                return Result.Failure<Unit>(CommonErrors.UnhandledException.New(lEx.Message));
            }
        }

        #endregion

        #region Save
        public async Task<IHttpResult<Unit>> SaveChangesAsync(CancellationToken aCancellationToken = default)
        {
            try
            {
                return !_context.ChangeTracker.HasChanges() || (await _context.SaveChangesAsync(aCancellationToken) > 0)
                    ? Result.SuccessHttp(Unit.Value)
                    : Result.Failure<Unit>(DBErrors.Repository.Save.Error);
            }
            catch (Exception lEx)
            {
                _logger.LogError(lEx, "An error occurred while saving DB changes: {ErrorMessage}", lEx.Message);
                return Result.Failure<Unit>(CommonErrors.UnhandledException.New(lEx.Message));
            }
        }

        public async Task<IHttpResult<Unit>> ShouldSaveChangesAsync(CancellationToken aCancellationToken = default)
        {
            try
            {
                if (!_context.ChangeTracker.HasChanges())
                    return Result.Failure<Unit>(DBErrors.Repository.Save.NoChanges);

                return await _context.SaveChangesAsync(aCancellationToken) > 0
                    ? Result.SuccessHttp(Unit.Value)
                    : Result.Failure<Unit>(DBErrors.Repository.Save.Error);
            }
            catch (Exception lEx)
            {
                _logger.LogError(lEx, "An error occurred while saving DB changes: {ErrorMessage}", lEx.Message);
                return Result.Failure<Unit>(CommonErrors.UnhandledException.New(lEx.Message));
            }
        }

        #endregion

        #endregion

    }
}
