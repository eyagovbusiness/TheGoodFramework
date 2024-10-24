using Ardalis.Specification;
using TGF.CA.Application.DTOs;

namespace TGF.CA.Application.Contracts.Services
{
    public interface IPagedListMapperService
    {
        /// <summary>
        /// Converts a collection of items to a PagedListDTO based on the provided specification.
        /// </summary>
        /// <typeparam name="TEntity">The type of the entity.</typeparam>
        /// <typeparam name="TDTO">The type of the DTO.</typeparam>
        /// <param name="items">The collection of items.</param>
        /// <param name="specification">The specification for paging and sorting.</param>
        /// <param name="totalItems">The total number of items.</param>
        /// <returns>A PagedListDTO containing the paged items.</returns>
        PagedListDTO<TDTO> ToPagedListDTO<TEntity, TDTO>(IEnumerable<TDTO> items, ISpecification<TEntity> specification, int totalItems) where TEntity : class;
    }
}