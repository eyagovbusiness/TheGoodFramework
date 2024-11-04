using TGF.Common.ROP.HttpResult;

namespace TGF.CA.Domain.Contracts.Repositories
{
    /// <summary>
    /// Interface of the base default CRUD implementations implemented in TGF.CA.Infrastructure.DB.Repository.RepositoryBase class.
    /// </summary>
    /// <remarks>Works with any not abstract class as entitiy. Does not contain default "by id" operations implementations</remarks>
    public interface IRepository<T> : ICommandRepository<T>, IQueryRepository<T>
        where T : class, new()
    {
        public new Task<IHttpResult<T>> AddAsync(T aEntity, CancellationToken aCancellationToken = default);
        public new Task<IHttpResult<T>> UpdateAsync(T aEntity, CancellationToken aCancellationToken = default);
        public new Task<IHttpResult<T>> DeleteAsync(T aEntity, CancellationToken aCancellationToken = default);
    }
}
