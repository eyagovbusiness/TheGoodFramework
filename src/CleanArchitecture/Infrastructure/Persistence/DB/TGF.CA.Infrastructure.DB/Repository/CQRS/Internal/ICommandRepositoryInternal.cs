using Microsoft.EntityFrameworkCore.Storage;
using TGF.CA.Domain.Contracts.Repositories;
using TGF.Common.ROP;
using TGF.Common.ROP.HttpResult;
#pragma warning disable CA1068 // CancellationToken parameters must come last
namespace TGF.CA.Infrastructure.DB.Repository.CQRS.Internal
{
    /// <summary>
    /// Provides a set of methods for executing commands in a write repository, handling CRUD operations, managing transactions, and saving changes(CQRS friendly).
    /// </summary>
    internal interface ICommandRepositoryInternal<T> : ICommandRepository<T>
        where T : class
    {
        #region Transactions
        /// <summary>
        /// Begins a transaction asynchronously.
        /// </summary>
        Task<IHttpResult<IDbContextTransaction>> BeginTransactionAsync(CancellationToken aCancellationToken = default);

        /// <summary>
        /// Commits a transaction asynchronously.
        /// </summary>
        Task<IHttpResult<Unit>> CommitTransactionAsync(IDbContextTransaction aTransaction, CancellationToken aCancellationToken = default);

        /// <summary>
        /// Rolls back a transaction asynchronously.
        /// </summary>
        Task<IHttpResult<Unit>> RollbackTransactionAsync(IDbContextTransaction aTransaction, CancellationToken aCancellationToken = default);

        #endregion

    }

}