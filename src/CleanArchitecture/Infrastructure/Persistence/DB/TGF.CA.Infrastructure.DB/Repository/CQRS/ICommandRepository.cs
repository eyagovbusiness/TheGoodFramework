using Microsoft.EntityFrameworkCore.Storage;
using TGF.Common.ROP;
using TGF.Common.ROP.HttpResult;
#pragma warning disable CA1068 // CancellationToken parameters must come last
namespace TGF.CA.Infrastructure.DB.Repository.CQRS
{
    public interface ICommandRepository
    {
        #region Command
        Task<IHttpResult<T>> TryCommandAsync<T>(Func<CancellationToken, Task<IHttpResult<T>>> aCommandAsyncAction, CancellationToken aCancellationToken = default, Func<int, T, IHttpResult<T>>? aSaveResultOverride = default);
        Task<IHttpResult<T>> TryCommandAsync<T>(Func<CancellationToken, Task<T>> aCommandAsyncAction, CancellationToken aCancellationToken = default, Func<int, T, IHttpResult<T>>? aSaveResultOverride = default);
        Task<IHttpResult<T>> TryCommandAsync<T>(Func<IHttpResult<T>> aCommandAction, CancellationToken aCancellationToken = default, Func<int, T, IHttpResult<T>>? aSaveResultOverride = default);
        Task<IHttpResult<T>> TryCommandAsync<T>(Func<T> aCommandAction, CancellationToken aCancellationToken = default, Func<int, T, IHttpResult<T>>? aSaveResultOverride = default);

        #endregion

        #region Create-Update-Delete
        Task<IHttpResult<T>> AddAsync<T>(T aEntity, CancellationToken aCancellationToken = default) where T : class;
        Task<IHttpResult<T>> UpdateAsync<T>(T aEntity, CancellationToken aCancellationToken = default) where T : class;
        Task<IHttpResult<T>> DeleteAsync<T>(T aEntity, CancellationToken aCancellationToken = default) where T : class;
        #endregion

        #region Transactions
        Task<IHttpResult<IDbContextTransaction>> BeginTransactionAsync(CancellationToken aCancellationToken = default);
        Task<IHttpResult<Unit>> CommitTransactionAsync(IDbContextTransaction aTransaction, CancellationToken aCancellationToken = default);
        Task<IHttpResult<Unit>> RollbackTransactionAsync(IDbContextTransaction aTransaction, CancellationToken aCancellationToken = default);
        #endregion

        #region Save
        Task<IHttpResult<T>> TrySaveChangesAsync<T>(T aResult, CancellationToken aCancellationToken = default, Func<int, T, IHttpResult<T>>? aSaveResultOverride = default);
        IHttpResult<T> DefaultSaveResultFunc<T>(int aChangeCount, T aCommandResult);
        #endregion

    }
}