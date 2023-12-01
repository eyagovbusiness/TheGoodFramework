using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using TGF.Common.ROP.Errors;
using TGF.Common.ROP.HttpResult;
using TGF.Common.ROP.Result;

namespace TGF.CA.Infrastructure.DB.Repository.CQRS
{
    public abstract class QueryRepositoryBase<TDbContext> : IQueryRepository
    where TDbContext : DbContext
    {
        protected readonly TDbContext _context;
        protected readonly ILogger<RepositoryBase<TDbContext>> _logger;

        public QueryRepositoryBase(TDbContext aContext, ILogger<RepositoryBase<TDbContext>> aLogger)
        {
            _context = aContext;
            _logger = aLogger;
        }
        #region IQueryRepository

        #region Query
        public async Task<IHttpResult<T>> TryQueryAsync<T>(Func<CancellationToken, Task<T>> aQueryAction, CancellationToken aCancellationToken = default)
        {
            try
            {
                return await Result.CancellationTokenResult(aCancellationToken)
                    .Map(async _ => await aQueryAction(aCancellationToken));
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
        {
            try
            {
                var lEntity = await _context.Set<T>().FindAsync(new object[] { aEntityId }, aCancellationToken);
                return lEntity != null ? Result.SuccessHttp(lEntity) : Result.Failure<T>(DBErrors.Repository.Entity.NotFound);
            }
            catch (Exception lEx)
            {
                _logger.LogError(lEx, "An error occurred while trying to get the entity by Id from DB: {ErrorMessage}", lEx.Message);
                return Result.Failure<T>(CommonErrors.UnhandledException.New(lEx.Message));
            }
        }

        #endregion

        #endregion

    }
}
