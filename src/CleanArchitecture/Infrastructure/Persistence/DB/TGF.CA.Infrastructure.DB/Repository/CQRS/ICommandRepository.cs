using Microsoft.EntityFrameworkCore.Storage;
using TGF.Common.ROP;
using TGF.Common.ROP.HttpResult;

namespace TGF.CA.Infrastructure.DB.Repository.CQRS
{
    public interface ICommandRepository
    {
        #region Command
        Task<IHttpResult<T>> TryCommandAsync<T>(Func<CancellationToken, Task<T>> aCommandAsyncAction, CancellationToken aCancellationToken = default);
        Task<IHttpResult<T>> TryCommandAsync<T>(Func<T> aCommandAction, CancellationToken aCancellationToken = default);
        #endregion

        #region Create-Update-Delete
        Task<IHttpResult<T>> AddAsync<T>(T aEntity, CancellationToken aCancellationToken = default) where T : class;
        Task<IHttpResult<Unit>> UpdateAsync<T>(T aEntity, CancellationToken aCancellationToken = default) where T : class;
        Task<IHttpResult<Unit>> DeleteAsync<T>(T aEntity, CancellationToken aCancellationToken = default) where T : class;
        #endregion

        #region Transactions
        Task<IHttpResult<IDbContextTransaction>> BeginTransactionAsync(CancellationToken aCancellationToken = default);
        Task<IHttpResult<Unit>> CommitTransactionAsync(IDbContextTransaction aTransaction, CancellationToken aCancellationToken = default);
        Task<IHttpResult<Unit>> RollbackTransactionAsync(IDbContextTransaction aTransaction, CancellationToken aCancellationToken = default);
        #endregion

        #region Save
        Task<IHttpResult<Unit>> SaveChangesAsync(CancellationToken aCancellationToken = default);
        Task<IHttpResult<Unit>> ShouldSaveChangesAsync(CancellationToken aCancellationToken = default);
        #endregion

    }
}