﻿using Ardalis.Specification;
using Ardalis.Specification.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;
using Microsoft.Extensions.Logging;
using TGF.CA.Domain.Contracts.Repositories;
using TGF.CA.Infrastructure.DB.DbContext;
using TGF.CA.Infrastructure.DB.Repository.CQRS;
using TGF.CA.Infrastructure.DB.Repository.CQRS.Internal;
using TGF.Common.ROP;
using TGF.Common.ROP.HttpResult;

namespace TGF.CA.Infrastructure.DB.Repository
{
    /// <summary>
    /// A base class for any read/write repository with native error handling logic using ROP.
    /// </summary>
    /// <typeparam name="TRepository">The type of the child class implementing this repository.</typeparam>
    /// <typeparam name="TDbContext">The type of the DbContext to use in this repository.</typeparam>
    public abstract class Repository<TRepository, TDbContext, T>(TDbContext aContext, ILogger<TRepository> aLogger, ISpecificationEvaluator specificationEvaluator)
        : ICommandRepository<T>, IQueryRepository<T>, IRepository<T>
        where TDbContext : Microsoft.EntityFrameworkCore.DbContext
        where TRepository : class
        where T : class
    {
        public Repository(TDbContext aContext, ILogger<TRepository> aLogger)
            : this(aContext, aLogger, SpecificationEvaluator.Default)
        {
            _context = aContext;
            _logger = aLogger;
        }

        private readonly InternalCommandRepository _commandRepository = new(aContext, aLogger);
        private readonly InternalQueryRepository _queryRepository = new(aContext, aLogger, specificationEvaluator);

        // Check if context implements IReadOnlyDbContext and return the appropriate IQueryable<T>

        protected readonly TDbContext _context = aContext;
        protected readonly ILogger<TRepository> _logger = aLogger;
        protected readonly ISpecificationEvaluator _specificationEvaluator = specificationEvaluator;
        protected IQueryable<T> Queryable
        => _context is IReadOnlyDbContext readOnlyDbContext
            ? readOnlyDbContext.Query<T>()
            : _context.Set<T>().AsQueryable();

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
        private class InternalCommandRepository : CommandRepository<TRepository, TDbContext, T>
        {
            internal InternalCommandRepository(TDbContext aContext, ILogger<TRepository> aLogger) : base(aContext, aLogger)
            {
            }
        }
        private class InternalQueryRepository : QueryRepositoryInternal<TRepository, TDbContext, T>
        {
            internal InternalQueryRepository(TDbContext aContext, ILogger<TRepository> aLogger, ISpecificationEvaluator specificationEvaluator) : base(aContext, aLogger, specificationEvaluator)
            {
            }
        }
        #endregion

    }

}