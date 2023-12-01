using Microsoft.EntityFrameworkCore;
using TGF.Common.ROP.HttpResult;
using TGF.Common.ROP;
using TGF.Common.ROP.Result;
using Microsoft.EntityFrameworkCore.Storage;
using TGF.Common.ROP.Errors;
using Microsoft.Extensions.Logging;
using TGF.CA.Infrastructure.DB.Repository.CQRS;

namespace TGF.CA.Infrastructure.DB.Repository
{
    public abstract class RepositoryBase<TDbContext>
        where TDbContext : DbContext
    {
        private readonly ICommandRepository _commandRepository;
        private readonly IQueryRepository _queryRepository;

        protected readonly TDbContext _context;
        protected readonly ILogger<RepositoryBase<TDbContext>> _logger;

        public RepositoryBase(TDbContext aContext, ILogger<RepositoryBase<TDbContext>> aLogger)
        {
            _commandRepository = new InternalCommandRepository(aContext, aLogger);
            _queryRepository = new InternalQueryRepository(aContext, aLogger);
            _context = aContext;
            _logger = aLogger;
        }

        #region Command-Query
        public async Task<IHttpResult<T>> TryQueryAsync<T>(Func<CancellationToken, Task<T>> aQueryAction, CancellationToken aCancellationToken = default)
            => await _queryRepository.TryQueryAsync(aQueryAction, aCancellationToken);
        public async Task<IHttpResult<T>> TryCommandAsync<T>(Func<CancellationToken, Task<T>> aCommandAsyncAction, CancellationToken aCancellationToken = default)
            => await _commandRepository.TryCommandAsync(aCommandAsyncAction, aCancellationToken);
        public async Task<IHttpResult<T>> TryCommandAsync<T>(Func<T> aCommandAction, CancellationToken aCancellationToken = default)
            => await _commandRepository.TryCommandAsync(aCommandAction, aCancellationToken);

        #endregion

        #region CRUD
        public virtual async Task<IHttpResult<T>> AddAsync<T>(T aEntity, CancellationToken aCancellationToken = default)
            where T : class
        => await _commandRepository.AddAsync(aEntity, aCancellationToken);

        public virtual async Task<IHttpResult<T>> GetByIdAsync<T>(object aEntityId, CancellationToken aCancellationToken = default)
            where T : class
        => await _queryRepository.GetByIdAsync<T>(aEntityId, aCancellationToken);

        public virtual async Task<IHttpResult<Unit>> UpdateAsync<T>(T aEntity, CancellationToken aCancellationToken = default) 
            where T : class
        => await _commandRepository.UpdateAsync(aEntity, aCancellationToken);

        public virtual async Task<IHttpResult<Unit>> DeleteAsync<T>(T aEntity, CancellationToken aCancellationToken = default)
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
        protected async Task<IHttpResult<Unit>> SaveChangesAsync(CancellationToken aCancellationToken = default)
            => await _commandRepository.SaveChangesAsync(aCancellationToken);

        protected async Task<IHttpResult<Unit>> ShouldSaveChangesAsync(CancellationToken aCancellationToken = default)
            => await _commandRepository.ShouldSaveChangesAsync(aCancellationToken);

        #endregion

        #region Private helper classes
        private class InternalCommandRepository : CommandRepositoryBase<TDbContext>
        {
            internal InternalCommandRepository(TDbContext aContext, ILogger<RepositoryBase<TDbContext>> aLogger) : base(aContext, aLogger)
            {
            }
        }
        private class InternalQueryRepository : QueryRepositoryBase<TDbContext>
        {
            internal InternalQueryRepository(TDbContext aContext, ILogger<RepositoryBase<TDbContext>> aLogger) : base(aContext, aLogger)
            {
            }
        }
        #endregion

    }

}
