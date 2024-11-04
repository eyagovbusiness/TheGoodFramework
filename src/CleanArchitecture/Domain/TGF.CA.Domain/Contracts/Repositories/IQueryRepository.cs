using Ardalis.Specification;
using TGF.CA.Domain.Contracts.Repositories.Base;
using TGF.Common.ROP.HttpResult;

namespace TGF.CA.Domain.Contracts.Repositories
{
    /// <summary>
    /// Interface for query repositorties working with any class as entities/>.
    /// Provides a set of methods for executing queries and retrieving entities in a read only repository(CQRS friendly).
    /// </summary>
    public interface IQueryRepository<T>
        : IQueryRepositoryBase<T>
        where T : class
    {

        #region Read

        /// <summary>
        /// Retrieves the list of entities from the database.
        /// </summary>
        /// <param name="specification">The specification used to query the databse.</param>
        /// <param name="cancellationToken">Cancellation token to cancel the query if needed.</param>
        /// <returns>All entities of type <see cref="T"/> from the database.</returns>
        Task<IHttpResult<IEnumerable<T>>> GetListAsync(ISpecification<T> specification, CancellationToken cancellationToken = default);

        /// <summary>
        /// Get the count of records in the DB for the <see cref="T"/> entitiy.
        /// </summary>
        /// <param name="cancellationToken"></param>
        /// <returns></returns>
        Task<IHttpResult<int>> GetCountAsync(CancellationToken cancellationToken = default);

        #endregion

    }
}
