using TGF.Common.ROP.HttpResult;

namespace TGF.CA.Infrastructure.DB.Repository.CQRS
{
    public interface IQueryRepository
    {
        Task<IHttpResult<T>> TryQueryAsync<T>(Func<CancellationToken, Task<IHttpResult<T>>> aQueryAsyncAction, CancellationToken aCancellationToken = default);
        Task<IHttpResult<T>> TryQueryAsync<T>(Func<CancellationToken, Task<T>> aQueryAsyncAction, CancellationToken aCancellationToken = default);
        IHttpResult<T> TryQuery<T>(Func<IHttpResult<T>> aQueryAction);
        IHttpResult<T> TryQuery<T>(Func<T> aQueryAction);

        Task<IHttpResult<T>> GetByIdAsync<T>(object aEntityId, CancellationToken aCancellationToken = default) where T : class;
    }
}