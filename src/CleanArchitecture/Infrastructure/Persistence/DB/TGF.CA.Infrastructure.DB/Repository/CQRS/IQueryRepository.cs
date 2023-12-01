using TGF.Common.ROP.HttpResult;

namespace TGF.CA.Infrastructure.DB.Repository.CQRS
{
    public interface IQueryRepository
    {
        Task<IHttpResult<T>> GetByIdAsync<T>(object aEntityId, CancellationToken aCancellationToken = default) where T : class;

        Task<IHttpResult<T>> TryQueryAsync<T>(Func<CancellationToken, Task<T>> aQueryAction, CancellationToken aCancellationToken = default);

    }
}