using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using TGF.Common.ROP.Errors;
using TGF.Common.ROP.HttpResult;
using TGF.Common.ROP.Result;

namespace TGF.CA.Infrastructure.DB.Repository.CQRS
{
    /// <summary>
    /// A base class for a CQRS read only repository with native error handling logic for Query operations using ROP.
    /// </summary>
    /// <typeparam name="TRepository">The type of the child class implementing this repository.</typeparam>
    /// <typeparam name="TDbContext">The type of the DbContext to use in this repository.</typeparam>
    public abstract class QueryRepositoryBase<TRepository,TDbContext> : IQueryRepository
    where TDbContext : DbContext
    where TRepository : class
    {
        protected readonly TDbContext _context;
        protected readonly ILogger<TRepository> _logger;

        public QueryRepositoryBase(TDbContext aContext, ILogger<TRepository> aLogger)
        {
            _context = aContext;
            _logger = aLogger;
        }
        #region IQueryRepository

        #region Query
        public async Task<IHttpResult<T>> TryQueryAsync<T>(Func<CancellationToken, Task<IHttpResult<T>>> aQueryAsyncAction, CancellationToken aCancellationToken = default)
        {
            try
            {
                return await Result.CancellationTokenResult(aCancellationToken)
                    .Bind(_ => aQueryAsyncAction(aCancellationToken));
            }
            catch (Exception lEx)
            {
                _logger.LogError(lEx, "An error occurred trying to execute a DB query at repository level : {ErrorMessage}", lEx.Message);
                return Result.Failure<T>(CommonErrors.UnhandledException.New(lEx.Message));
            }
        }

        public async Task<IHttpResult<T>> TryQueryAsync<T>(Func<CancellationToken, Task<T>> aQueryAsyncAction, CancellationToken aCancellationToken = default)
        {
            try
            {
                return await Result.CancellationTokenResult(aCancellationToken)
                    .Map(async _ => await aQueryAsyncAction(aCancellationToken));
            }
            catch (Exception lEx)
            {
                _logger.LogError(lEx, "An error occurred trying to execute a DB query at repository level : {ErrorMessage}", lEx.Message);
                return Result.Failure<T>(CommonErrors.UnhandledException.New(lEx.Message));
            }
        }

        public IHttpResult<T> TryQuery<T>(Func<IHttpResult<T>> aQueryAction)
        {
            try
            {
                return aQueryAction();
            }
            catch (Exception lEx)
            {
                _logger.LogError(lEx, "An error occurred trying to execute a DB query at repository level : {ErrorMessage}", lEx.Message);
                return Result.Failure<T>(CommonErrors.UnhandledException.New(lEx.Message));
            }
        }

        public IHttpResult<T> TryQuery<T>(Func<T> aQueryAction)
        {
            try
            {
                return Result.SuccessHttp(aQueryAction());
            }
            catch (Exception lEx)
            {
                _logger.LogError(lEx, "An error occurred trying to execute a DB query at repository level : {ErrorMessage}", lEx.Message);
                return Result.Failure<T>(CommonErrors.UnhandledException.New(lEx.Message));
            }
        }

        #endregion

        #region Read
        public virtual async Task<IHttpResult<T>> GetByIdAsync<T>(object aEntityId, CancellationToken aCancellationToken = default)
            where T : class
        => await TryQueryAsync(async (aCancellationToken) =>
        {
            var lEntity = await _context.Set<T>().FindAsync(new object[] { aEntityId }, aCancellationToken);
            return lEntity != null ? Result.SuccessHttp(lEntity!) : Result.Failure<T>(DBErrors.Repository.Entity.NotFound);

        }, aCancellationToken);

        #endregion

        #endregion

    }
}
