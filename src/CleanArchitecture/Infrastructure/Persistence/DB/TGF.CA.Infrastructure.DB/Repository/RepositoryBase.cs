using Microsoft.EntityFrameworkCore.Storage;
using Microsoft.Extensions.Logging;
using TGF.CA.Domain.Contracts;
using TGF.CA.Domain.Contracts.Repositories;
using TGF.CA.Infrastructure.DB.Repository.CQRS;
using TGF.Common.ROP;
using TGF.Common.ROP.HttpResult;

namespace TGF.CA.Infrastructure.DB.Repository
{
    /// <summary>
    /// A base class for any read/write repository with native error handling logic using ROP.
    /// </summary>
    /// <typeparam name="TRepository">The type of the child class implementing this repository.</typeparam>
    /// <typeparam name="TDbContext">The type of the DbContext to use in this repository.</typeparam>
    public abstract class RepositoryBase<TRepository, TDbContext> : ICommandRepository, IQueryRepository, IRepositoryBase
    where TDbContext : Microsoft.EntityFrameworkCore.DbContext
    where TRepository : class
    {
        private readonly ICommandRepository _commandRepository;
        private readonly IQueryRepository _queryRepository;

        protected readonly TDbContext _context;
        protected readonly ILogger<TRepository> _logger;

        public RepositoryBase(TDbContext aContext, ILogger<TRepository> aLogger)
        {
            _commandRepository = new InternalCommandRepository(aContext, aLogger);
            _queryRepository = new InternalQueryRepository(aContext, aLogger);
            _context = aContext;
            _logger = aLogger;
        }

        #region Command-Query
        public async Task<IHttpResult<T>> TryCommandAsync<T>(Func<CancellationToken, Task<IHttpResult<T>>> aCommandAsyncAction, CancellationToken aCancellationToken = default, Func<int, T, IHttpResult<T>>? aSaveResultOverride = default)
            => await _commandRepository.TryCommandAsync(aCommandAsyncAction, aCancellationToken, aSaveResultOverride);
        public async Task<IHttpResult<T>> TryCommandAsync<T>(Func<CancellationToken, Task<T>> aCommandAsyncAction, CancellationToken aCancellationToken = default, Func<int, T, IHttpResult<T>>? aSaveResultOverride = default)
            => await _commandRepository.TryCommandAsync(aCommandAsyncAction, aCancellationToken, aSaveResultOverride);
        public async Task<IHttpResult<T>> TryCommandAsync<T>(Func<IHttpResult<T>> aCommandAction, CancellationToken aCancellationToken = default, Func<int, T, IHttpResult<T>>? aSaveResultOverride = default)
            => await _commandRepository.TryCommandAsync(aCommandAction, aCancellationToken, aSaveResultOverride);
        public async Task<IHttpResult<T>> TryCommandAsync<T>(Func<T> aCommandAction, CancellationToken aCancellationToken = default, Func<int, T, IHttpResult<T>>? aSaveResultOverride = default)
            => await _commandRepository.TryCommandAsync(aCommandAction, aCancellationToken, aSaveResultOverride);

        public async Task<IHttpResult<T>> TryQueryAsync<T>(Func<CancellationToken, Task<IHttpResult<T>>> aQueryAsyncAction, CancellationToken aCancellationToken = default)
            => await _queryRepository.TryQueryAsync(aQueryAsyncAction, aCancellationToken);
        public async Task<IHttpResult<T>> TryQueryAsync<T>(Func<CancellationToken, Task<T>> aQueryAction, CancellationToken aCancellationToken = default)
            => await _queryRepository.TryQueryAsync(aQueryAction, aCancellationToken);
        public IHttpResult<T> TryQuery<T>(Func<IHttpResult<T>> aQueryAction)
            => _queryRepository.TryQuery(aQueryAction);
        public IHttpResult<T> TryQuery<T>(Func<T> aQueryAction)
            => _queryRepository.TryQuery(aQueryAction);

        #endregion

        #region CRUD
        public virtual async Task<IHttpResult<T>> AddAsync<T>(T aEntity, CancellationToken aCancellationToken = default)
            where T : class
        => await _commandRepository.AddAsync(aEntity, aCancellationToken);

        public virtual async Task<IHttpResult<T>> GetByIdAsync<T, TKey>(TKey aEntityId, CancellationToken aCancellationToken = default)
            where T : class
            where TKey : struct, IEquatable<TKey>
        => await _queryRepository.GetByIdAsync<T, TKey>(aEntityId, aCancellationToken);

        public virtual async Task<IHttpResult<List<T>>> GetByIdListAsync<T, TKey>(IEnumerable<TKey> aEntityIdList, CancellationToken aCancellationToken = default)
            where T : class, IEntity<TKey>
            where TKey : struct, IEquatable<TKey>
        => await _queryRepository.GetByIdListAsync<T, TKey>(aEntityIdList, aCancellationToken);

        public virtual async Task<IHttpResult<T>> UpdateAsync<T>(T aEntity, CancellationToken aCancellationToken = default)
            where T : class
        => await _commandRepository.UpdateAsync(aEntity, aCancellationToken);

        public virtual async Task<IHttpResult<T>> DeleteAsync<T>(T aEntity, CancellationToken aCancellationToken = default)
            where T : class
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
        public virtual async Task<IHttpResult<T>> TrySaveChangesAsync<T>(T aResult, CancellationToken aCancellationToken = default, Func<int, T, IHttpResult<T>>? aSaveResultOverride = default)
            => await _commandRepository.TrySaveChangesAsync(aResult, aCancellationToken, aSaveResultOverride);
        public virtual IHttpResult<T> DefaultSaveResultFunc<T>(int aChangeCount, T aCommandResult)
            => _commandRepository.DefaultSaveResultFunc(aChangeCount, aCommandResult);

        #endregion

        #region Private helper classes
        private class InternalCommandRepository : CommandRepositoryBase<TRepository, TDbContext>
        {
            internal InternalCommandRepository(TDbContext aContext, ILogger<TRepository> aLogger) : base(aContext, aLogger)
            {
            }
        }
        private class InternalQueryRepository : QueryRepositoryBase<TRepository, TDbContext>
        {
            internal InternalQueryRepository(TDbContext aContext, ILogger<TRepository> aLogger) : base(aContext, aLogger)
            {
            }
        }
        #endregion

    }

}
