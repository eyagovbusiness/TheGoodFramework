using TGF.Common.ROP.HttpResult;

namespace TGF.CA.Domain.Contracts.Repositories
{
    /// <summary>
    /// Interface of the base default CRUD implementations implemented in TGF.CA.Infrastructure.DB.Repository.RepositoryBase class.
    /// </summary>
    /// <remarks>Works only with <see cref=" IEntity{TKey}"/> as entity type. Includes by Id operations.</remarks>
    public interface IEntitiyRepository<T, TKey> : IEntitiyCommandRepository<T, TKey>, IEntityQueryRepository<T, TKey>
        where T : class, IEntity<TKey>, new()
        where TKey : struct, IEquatable<TKey>
    {
        public new Task<IHttpResult<T>> AddAsync(T aEntity, CancellationToken aCancellationToken = default);
        public new Task<IHttpResult<T>> GetByIdAsync(TKey aEntityId, CancellationToken aCancellationToken = default);
        public new Task<IHttpResult<IEnumerable<T>>> GetByIdListAsync(IEnumerable<TKey> aEntityIdList, CancellationToken aCancellationToken = default);
        public new Task<IHttpResult<T>> UpdateAsync(T aEntity, CancellationToken aCancellationToken = default);
        public new Task<IHttpResult<T>> DeleteAsync(T aEntity, CancellationToken aCancellationToken = default);
    }
}
