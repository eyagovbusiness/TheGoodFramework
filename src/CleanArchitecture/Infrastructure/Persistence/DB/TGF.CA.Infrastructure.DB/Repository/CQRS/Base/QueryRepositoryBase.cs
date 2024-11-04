using Ardalis.Specification;
using Ardalis.Specification.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using TGF.CA.Infrastructure.DB.DbContext;
using TGF.Common.ROP.Errors;
using TGF.Common.ROP.HttpResult;
using TGF.Common.ROP.HttpResult.RailwaySwitches;
using TGF.Common.ROP.Result;

namespace TGF.CA.Infrastructure.DB.Repository.CQRS.Base
{

    /// <summary>
    /// Base class for quiery repositories with the TryQuery result abstractions.
    /// </summary>
    public abstract class QueryRepositoryBase<TRepository, TDbContext, T>(
    TDbContext aContext,
    ILogger<TRepository> aLogger,
    ISpecificationEvaluator specificationEvaluator)
    where TDbContext : Microsoft.EntityFrameworkCore.DbContext
    where TRepository : class
    where T : class
    {

        protected readonly TDbContext _context = aContext;
        protected readonly ILogger<TRepository> _logger = aLogger;
        protected readonly ISpecificationEvaluator _specificationEvaluator = specificationEvaluator;

        public QueryRepositoryBase(TDbContext aContext, ILogger<TRepository> aLogger)
            : this(aContext, aLogger, SpecificationEvaluator.Default) {
            _context = aContext;
            _logger = aLogger;
        }

        // Check if context implements IReadOnlyDbContext and return the appropriate IQueryable<T>
        protected IQueryable<T> Queryable 
        => _context is IReadOnlyDbContext readOnlyDbContext
            ? readOnlyDbContext.Query<T>()
            : _context.Set<T>().AsQueryable();

        #region Query
        public async Task<IHttpResult<TResult>> TryQueryAsync<TResult>(Func<CancellationToken, Task<IHttpResult<TResult>>> aQueryAsyncAction, CancellationToken aCancellationToken = default) {
            try {
                return await Result.CancellationTokenResult(aCancellationToken)
                    .Bind(_ => aQueryAsyncAction(aCancellationToken));
            }
            catch (Exception lEx) {
                return GetQueryExceptionResult<TResult>(lEx);
            }
        }

        public async Task<IHttpResult<TResult>> TryQueryAsync<TResult>(Func<CancellationToken, Task<TResult>> aQueryAsyncAction, CancellationToken aCancellationToken = default) {
            try {
                return await Result.CancellationTokenResult(aCancellationToken)
                    .Map(async _ => await aQueryAsyncAction(aCancellationToken));
            }
            catch (Exception lEx) {
                return GetQueryExceptionResult<TResult>(lEx);
            }
        }

        public IHttpResult<TResult> TryQuery<TResult>(Func<IHttpResult<TResult>> aQueryAction) {
            try {
                return aQueryAction();
            }
            catch (Exception lEx) {
                return GetQueryExceptionResult<TResult>(lEx);
            }
        }

        public IHttpResult<TResult> TryQuery<TResult>(Func<TResult> aQueryAction) {
            try {
                return Result.SuccessHttp(aQueryAction());
            }
            catch (Exception lEx) {
                return GetQueryExceptionResult<TResult>(lEx);
            }
        }

        private IHttpResult<TResult> GetQueryExceptionResult<TResult>(Exception exception) {
            const string errorMessageTemplate = "An error occurred trying to execute a DB query in {TRepository} for entity {EntityName}: {ExceptionMessage}";
            _logger.LogError(exception, errorMessageTemplate, nameof(TRepository), nameof(TResult), exception.Message);

            var errorMessage = $"An error occurred trying to execute a DB query in {nameof(TRepository)} for entity {nameof(TResult)}: {exception.Message}";
            return Result.Failure<TResult>(CommonErrors.UnhandledException.New(errorMessage));
        }


        #endregion

        #region Read

        public virtual async Task<IHttpResult<IEnumerable<T>>> GetListAsync(CancellationToken cancellationToken = default)
        => await TryQueryAsync(async cancellationToken => {
            var entities = await Queryable.ToListAsync(cancellationToken);
            return entities.Count != 0 ? Result.SuccessHttp(entities as IEnumerable<T>) : Result.Failure<IEnumerable<T>>(DBErrors.Repository.Entity.NotFound);
        }, cancellationToken);

        public virtual async Task<IHttpResult<IEnumerable<T>>> GetListAsync(ISpecification<T> specification, CancellationToken cancellationToken = default)
        => await TryQueryAsync(async cancellationToken => {
            var result = await _specificationEvaluator.GetQuery(Queryable, specification).ToListAsync(cancellationToken);
            return Result.SuccessHttp(result as IEnumerable<T>);
        }, cancellationToken);

        public virtual async Task<IHttpResult<int>> GetCountAsync(CancellationToken cancellationToken = default)
        => await TryQueryAsync(async cancellationToken =>
            Result.SuccessHttp(await Queryable.CountAsync(cancellationToken)), cancellationToken);
        #endregion

    }

}
