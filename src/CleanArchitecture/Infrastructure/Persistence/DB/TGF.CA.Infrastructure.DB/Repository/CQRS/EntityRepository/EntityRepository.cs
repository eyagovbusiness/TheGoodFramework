using Ardalis.Specification;
using Ardalis.Specification.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;
using Microsoft.Extensions.Logging;
using TGF.CA.Domain.Contracts.Repositories.EntityRepository;
using TGF.CA.Infrastructure.DB.Repository.CQRS.Internal;
using TGF.Common.ROP;
using TGF.Common.ROP.HttpResult;

namespace TGF.CA.Infrastructure.DB.Repository.CQRS.EntityRepository
{
    /// <summary>
    /// A base class for any read/write repository with native error handling logic using ROP.
    /// </summary>
    /// <typeparam name="TRepository">The type of the child class implementing this repository.</typeparam>
    /// <typeparam name="TDbContext">The type of the DbContext to use in this repository.</typeparam>
    public abstract class EntityRepository<TRepository, TDbContext, T, TKey>(TDbContext aContext, ILogger<TRepository> aLogger, ISpecificationEvaluator specificationEvaluator)
        : IEntityCommandRepository<T, TKey>, IEntityQueryRepository<T, TKey>, IEntityRepository<T, TKey>
        where TDbContext : Microsoft.EntityFrameworkCore.DbContext
        where TRepository : class
        where T : class, Domain.Contracts.IEntity<TKey>
        where TKey : struct, IEquatable<TKey>
    {
        public EntityRepository(TDbContext aContext, ILogger<TRepository> aLogger)
            : this(aContext, aLogger, SpecificationEvaluator.Default)
        {
            _context = aContext;
            _logger = aLogger;
        }

        private readonly InternalEntityCommandRepository _commandRepository = new(aContext, aLogger);
        private readonly InternalEntityQueryRepository _queryRepository = new(aContext, aLogger, specificationEvaluator);

        protected readonly TDbContext _context = aContext;
        protected readonly ILogger<TRepository> _logger = aLogger;
        protected readonly ISpecificationEvaluator _specificationEvaluator = specificationEvaluator;

        #region Command-Query
        public async Task<IHttpResult<TResult>> TryCommandAsync<TResult>(Func<CancellationToken, Task<IHttpResult<TResult>>> aCommandAsyncAction, Func<int, TResult, IHttpResult<TResult>>? aSaveResultOverride = default, CancellationToken aCancellationToken = default)
        => await _commandRepository.TryCommandAsync(aCommandAsyncAction, aSaveResultOverride, aCancellationToken);
        public async Task<IHttpResult<TResult>> TryCommandAsync<TResult>(Func<CancellationToken, Task<TResult>> aCommandAsyncAction, Func<int, TResult, IHttpResult<TResult>>? aSaveResultOverride = default, CancellationToken aCancellationToken = default)
        => await _commandRepository.TryCommandAsync(aCommandAsyncAction, aSaveResultOverride, aCancellationToken);
        public async Task<IHttpResult<TResult>> TryCommandAsync<TResult>(Func<IHttpResult<TResult>> aCommandAction, Func<int, TResult, IHttpResult<TResult>>? aSaveResultOverride = default, CancellationToken aCancellationToken = default)
        => await _commandRepository.TryCommandAsync(aCommandAction, aSaveResultOverride, aCancellationToken);
        public async Task<IHttpResult<TResult>> TryCommandAsync<TResult>(Func<TResult> aCommandAction, Func<int, TResult, IHttpResult<TResult>>? aSaveResultOverride = default, CancellationToken aCancellationToken = default)
        => await _commandRepository.TryCommandAsync(aCommandAction, aSaveResultOverride, aCancellationToken);
        public async Task<IHttpResult<TResult>> TryQueryAsync<TResult>(Func<CancellationToken, Task<IHttpResult<TResult>>> aQueryAsyncAction, CancellationToken aCancellationToken = default)
        => await _queryRepository.TryQueryAsync(aQueryAsyncAction, aCancellationToken);
        public async Task<IHttpResult<TResult>> TryQueryAsync<TResult>(Func<CancellationToken, Task<TResult>> aQueryAction, CancellationToken aCancellationToken = default)
        => await _queryRepository.TryQueryAsync(aQueryAction, aCancellationToken);
        public IHttpResult<TResult> TryQuery<TResult>(Func<IHttpResult<TResult>> aQueryAction)
        => _queryRepository.TryQuery(aQueryAction);
        public IHttpResult<TResult> TryQuery<TResult>(Func<TResult> aQueryAction)
        => _queryRepository.TryQuery(aQueryAction);

        #endregion

        #region CRUD
        public virtual async Task<IHttpResult<T>> AddAsync(T aEntity, CancellationToken aCancellationToken = default)
        => await _commandRepository.AddAsync(aEntity, aCancellationToken);

        public virtual async Task<IHttpResult<T>> GetByIdAsync(TKey aEntityId, CancellationToken aCancellationToken = default)
        => await _queryRepository.GetByIdAsync(aEntityId, aCancellationToken);

        public virtual async Task<IHttpResult<T>> GetByIdAsync(TKey aEntityId, ISpecification<T>? specification = default, CancellationToken aCancellationToken = default)
        => await _queryRepository.GetByIdAsync(aEntityId, specification, aCancellationToken);

        public virtual async Task<IHttpResult<IEnumerable<T>>> GetByIdListAsync(IEnumerable<TKey> aEntityIdList, CancellationToken aCancellationToken = default)
        => await _queryRepository.GetByIdListAsync(aEntityIdList, aCancellationToken);

        public virtual async Task<IHttpResult<IEnumerable<T>>> GetByIdListAsync(IEnumerable<TKey> aEntityIdList, ISpecification<T>? specification, CancellationToken aCancellationToken = default)
        => await _queryRepository.GetByIdListAsync(aEntityIdList, specification, aCancellationToken);

        public virtual async Task<IHttpResult<IEnumerable<T>>> GetListAsync(ISpecification<T> specification, CancellationToken cancellationToken = default)
        => await _queryRepository.GetListAsync(specification, cancellationToken);

        public async Task<IHttpResult<int>> GetCountAsync(CancellationToken cancellationToken = default)
        => await _queryRepository.GetCountAsync(cancellationToken);

        public virtual async Task<IHttpResult<T>> UpdateAsync(T aEntity, CancellationToken aCancellationToken = default)
        => await _commandRepository.UpdateAsync(aEntity, aCancellationToken);

        public virtual async Task<IHttpResult<T>> DeleteAsync(T aEntity, CancellationToken aCancellationToken = default)
        => await _commandRepository.DeleteAsync(aEntity, aCancellationToken);

        #endregion

        #region Transaction Management
        public async Task<IHttpResult<IDbContextTransaction>> BeginTransactionAsync(CancellationToken aCancellationToken = default)
        => await _commandRepository.BeginTransactionAsync(aCancellationToken);

        public async Task<IHttpResult<Unit>> CommitTransactionAsync(IDbContextTransaction aTransaction, CancellationToken aCancellationToken = default)
        => await _commandRepository.CommitTransactionAsync(aTransaction, aCancellationToken);

        public async Task<IHttpResult<Unit>> RollbackTransactionAsync(IDbContextTransaction aTransaction, CancellationToken aCancellationToken = default)
        => await _commandRepository.RollbackTransactionAsync(aTransaction, aCancellationToken);

        #endregion

        #region Save

        public async Task<IHttpResult<TResult>> TrySaveChangesAsync<TResult>(TResult aResult, Func<int, TResult, IHttpResult<TResult>>? aSaveResultOverride = null, CancellationToken aCancellationToken = default)
        => await _commandRepository.TrySaveChangesAsync(aResult, aSaveResultOverride, aCancellationToken);

        public virtual IHttpResult<TResult> DefaultSaveResultFunc<TResult>(int aChangeCount, TResult aCommandResult)
        => _commandRepository.DefaultSaveResultFunc(aChangeCount, aCommandResult);

        #endregion

        #region Private helper classes
        private class InternalEntityCommandRepository : EntityCommandRepository<TRepository, TDbContext, T, TKey>
        {
            internal InternalEntityCommandRepository(TDbContext aContext, ILogger<TRepository> aLogger) : base(aContext, aLogger)
            {
            }
        }
        private class InternalEntityQueryRepository : EntityQueryRepositoryInternal<TRepository, TDbContext, T, TKey>
        {
            internal InternalEntityQueryRepository(TDbContext aContext, ILogger<TRepository> aLogger, ISpecificationEvaluator specificationEvaluator) : base(aContext, aLogger, specificationEvaluator)
            {
            }
        }
        #endregion

    }

}
