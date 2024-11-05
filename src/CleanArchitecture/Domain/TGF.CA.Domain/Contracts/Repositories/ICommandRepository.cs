using TGF.CA.Domain.Contracts.Repositories.Base;
using TGF.Common.ROP.HttpResult;

namespace TGF.CA.Domain.Contracts.Repositories
{
    /// <summary>
    /// Interface for command repositorties working with any class as entities/>.
    /// Provides a set of methods for executing commands in a write repository, handling CRUD operations, managing transactions, and saving changes(CQRS friendly).
    /// </summary>
    public interface ICommandRepository<T>
       :ICommandRepositoryBase<T>
       where T : class
    {

        #region Create-Update-Delete
        /// <summary>
        /// Asynchronously adds an entity to the repository.
        /// </summary>
        Task<IHttpResult<T>> AddAsync(T aEntity, CancellationToken aCancellationToken = default);
        /// <summary>
        /// Asynchronously updates an entity in the repository.
        /// </summary>
        Task<IHttpResult<T>> UpdateAsync(T aEntity, CancellationToken aCancellationToken = default);
        /// <summary>
        /// Asynchronously deletes an entity from the repository.
        /// </summary>
        Task<IHttpResult<T>> DeleteAsync(T aEntity, CancellationToken aCancellationToken = default);
        #endregion

    }
}
