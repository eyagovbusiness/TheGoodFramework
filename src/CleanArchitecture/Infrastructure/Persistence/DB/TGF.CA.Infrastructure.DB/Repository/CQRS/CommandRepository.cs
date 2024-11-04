using Microsoft.Extensions.Logging;
using TGF.CA.Infrastructure.DB.Repository.CQRS.Base;
using TGF.CA.Infrastructure.DB.Repository.CQRS.Internal;
using TGF.Common.ROP.HttpResult;

namespace TGF.CA.Infrastructure.DB.Repository.CQRS {
    /// <summary>
    /// A base class for a CQRS write repository with native error handling logic for Command operations using ROP.
    /// </summary>
    /// <typeparam name="TRepository">The type of the child class implementing this repository.</typeparam>
    /// <typeparam name="TDbContext">The type of the DbContext to use in this repository.</typeparam>
    public abstract class CommandRepository<TRepository, TDbContext, T>(TDbContext aContext, ILogger<TRepository> aLogger) 
        : CommandRepositoryBase<TRepository, TDbContext>(aContext, aLogger), ICommandRepositoryInternal <T>
        where TDbContext : Microsoft.EntityFrameworkCore.DbContext
        where TRepository : class
        where T : class
    {
        protected readonly TDbContext context = aContext;
        protected readonly ILogger<TRepository> logger = aLogger;

        #region Create-Update-Delete
        public virtual async Task<IHttpResult<T>> AddAsync(T aEntity, CancellationToken aCancellationToken = default)
        => await TryCommandAsync(async (aCancellationToken) => (await context.Set<T>().AddAsync(aEntity, aCancellationToken)).Entity, aCancellationToken: aCancellationToken);

        public virtual async Task<IHttpResult<T>> UpdateAsync(T aEntity, CancellationToken aCancellationToken = default)
        => await TryCommandAsync(() => context.Set<T>().Update(aEntity).Entity, aCancellationToken: aCancellationToken);

        public virtual async Task<IHttpResult<T>> DeleteAsync(T aEntity, CancellationToken aCancellationToken = default)
        => await TryCommandAsync(() => context.Set<T>().Remove(aEntity).Entity, aCancellationToken: aCancellationToken);

        #endregion

    }
}
