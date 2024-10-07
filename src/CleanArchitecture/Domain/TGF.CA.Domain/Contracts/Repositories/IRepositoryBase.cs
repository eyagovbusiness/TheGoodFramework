using TGF.Common.ROP.HttpResult;

namespace TGF.CA.Domain.Contracts.Repositories
{
    /// <summary>
    /// Interface of the base default CRUD implementations implemented in TGF.CA.Infrastructure.DB.Repository.RepositoryBase class.
    /// </summary>
    public interface IRepositoryBase<T, TKey> : ICommandRepository<T, TKey>, IQueryRepository<T, TKey>
        where T : class, IEntity<TKey>
        where TKey : struct, IEquatable<TKey>
    {
        public new Task<IHttpResult<T>> AddAsync(T aEntity, CancellationToken aCancellationToken = default);
        public new Task<IHttpResult<T>> GetByIdAsync(TKey aEntityId, CancellationToken aCancellationToken = default);
        public new Task<IHttpResult<IEnumerable<T>>> GetByIdListAsync(IEnumerable<TKey> aEntityIdList, CancellationToken aCancellationToken = default);
        public new Task<IHttpResult<T>> UpdateAsync(T aEntity, CancellationToken aCancellationToken = default);
        public new Task<IHttpResult<T>> DeleteAsync(T aEntity, CancellationToken aCancellationToken = default);
    }
}
