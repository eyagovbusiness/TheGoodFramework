using Microsoft.EntityFrameworkCore.Storage;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using TGF.Common.ROP.Errors;
using TGF.Common.ROP.HttpResult;
using TGF.Common.ROP;
using TGF.Common.ROP.Result;

namespace TGF.CA.Infrastructure.DB.Repository.CQRS
{
    public abstract class CommandRepositoryBase<TRepository, TDbContext> : ICommandRepository
    where TDbContext : DbContext
    where TRepository : class
    {
        protected readonly TDbContext _context;
        protected readonly ILogger<TRepository> _logger;

        public CommandRepositoryBase(TDbContext aContext, ILogger<TRepository> aLogger)
        {
            _context = aContext;
            _logger = aLogger;
        }

        #region ICommandRepository

        #region Command
        public async Task<IHttpResult<T>> TryCommandAsync<T>(Func<CancellationToken, Task<IHttpResult<T>>> aCommandAsyncAction, CancellationToken aCancellationToken = default)
        {
            try
            {
                return await Result.CancellationTokenResult(aCancellationToken)
                    .Bind(_ => aCommandAsyncAction(aCancellationToken))
                    .Bind(commandResult => SaveChangesAsync(commandResult, aCancellationToken));
            }
            catch (Exception lEx)
            {
                _logger.LogError(lEx, "An error occurred trying to execute a DB command at repository level : {ErrorMessage}", lEx.Message);
                return Result.Failure<T>(CommonErrors.UnhandledException.New(lEx.Message));
            }
        }

        public async Task<IHttpResult<T>> TryCommandAsync<T>(Func<CancellationToken, Task<T>> aCommandAsyncAction, CancellationToken aCancellationToken = default)
        {
            try
            {
                return await Result.CancellationTokenResult(aCancellationToken)
                    .Map(_ => aCommandAsyncAction(aCancellationToken))
                    .Bind(commandResult => SaveChangesAsync(commandResult, aCancellationToken));
            }
            catch (Exception lEx)
            {
                _logger.LogError(lEx, "An error occurred trying to execute a DB command at repository level : {ErrorMessage}", lEx.Message);
                return Result.Failure<T>(CommonErrors.UnhandledException.New(lEx.Message));
            }
        }

        public async Task<IHttpResult<T>> TryCommandAsync<T>(Func<IHttpResult<T>> aCommandAction, CancellationToken aCancellationToken = default)
        {
            try
            {
                return await Result.CancellationTokenResult(aCancellationToken)
                    .Bind(_ => aCommandAction())
                    .Bind(commandResult => SaveChangesAsync(commandResult, aCancellationToken));
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
                return await Result.CancellationTokenResult(aCancellationToken)
                    .Map(_ => aCommandAction())
                    .Bind(commandResult => SaveChangesAsync(commandResult, aCancellationToken));
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
        => await TryCommandAsync(async (aCancellationToken) => (await _context.Set<T>().AddAsync(aEntity, aCancellationToken)).Entity, aCancellationToken);

        public virtual async Task<IHttpResult<T>> UpdateAsync<T>(T aEntity, CancellationToken aCancellationToken = default)
            where T : class
        => await TryCommandAsync(() => _context.Set<T>().Update(aEntity).Entity, aCancellationToken);

        public virtual async Task<IHttpResult<T>> DeleteAsync<T>(T aEntity, CancellationToken aCancellationToken = default)
            where T : class
        => await TryCommandAsync(() => _context.Set<T>().Remove(aEntity).Entity, aCancellationToken);

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
        public async Task<IHttpResult<T>> SaveChangesAsync<T>(T aResult, CancellationToken aCancellationToken = default)
        {
            try
            {
                return !_context.ChangeTracker.HasChanges() || (await _context.SaveChangesAsync(aCancellationToken) > 0)
                    ? Result.SuccessHttp(aResult)
                    : Result.Failure<T>(DBErrors.Repository.Save.Error);
            }
            catch (Exception lEx)
            {
                _logger.LogError(lEx, "An error occurred while saving DB changes: {ErrorMessage}", lEx.Message);
                return Result.Failure<T>(CommonErrors.UnhandledException.New(lEx.Message));
            }
        }

        public async Task<IHttpResult<T>> ShouldSaveChangesAsync<T>(T aResult, CancellationToken aCancellationToken = default)
        {
            try
            {
                if (!_context.ChangeTracker.HasChanges())
                    return Result.Failure<T>(DBErrors.Repository.Save.NoChanges);

                return await _context.SaveChangesAsync(aCancellationToken) > 0
                    ? Result.SuccessHttp(aResult)
                    : Result.Failure<T>(DBErrors.Repository.Save.Error);
            }
            catch (Exception lEx)
            {
                _logger.LogError(lEx, "An error occurred while saving DB changes: {ErrorMessage}", lEx.Message);
                return Result.Failure<T>(CommonErrors.UnhandledException.New(lEx.Message));
            }
        }

        #endregion

        #endregion

    }
}
