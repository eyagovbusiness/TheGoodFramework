using Ardalis.Specification;
using TGF.CA.Application.Contracts.Services;
using TGF.CA.Application.DTOs;

namespace TGF.CA.Application.Specifications
{
    /// <summary>
    /// Provides extension methods for mapping items to PagedListDTO.
    /// </summary>
    public class PagedListMapperService : IPagedListMapperService
    {
        public PagedListDTO<TDTO> ToPagedListDTO<TEntity, TDTO>(IEnumerable<TDTO> items, ISpecification<TEntity> specification, int totalItems)
            where TEntity : class
        {
            return specification switch
            {
                SortedAndPagedSpecification<TEntity> sortedAndPagedSpec => ToPagedListDTO(items, sortedAndPagedSpec, totalItems),
                PagedSpecification<TEntity> pagedSpec => ToPagedListDTO(items, pagedSpec, totalItems),
                _ => new PagedListDTO<TDTO>(1, 1, totalItems, totalItems, items.ToArray())
            };
        }

        /// <summary>
        /// Converts a collection of items to a PagedListDTO based on the provided paged specification.
        /// </summary>
        /// <typeparam name="TEntity">The type of the entity.</typeparam>
        /// <typeparam name="TDTO">The type of the DTO.</typeparam>
        /// <param name="items">The collection of items.</param>
        /// <param name="pagedSpecification">The paged specification.</param>
        /// <param name="totalItems">The total number of items.</param>
        /// <returns>A PagedListDTO containing the paged items.</returns>
        public static PagedListDTO<TDTO> ToPagedListDTO<TEntity, TDTO>(IEnumerable<TDTO> items, PagedSpecification<TEntity> pagedSpecification, int totalItems)
            where TEntity : class
        {
            return CreatePagedListDTO(items, pagedSpecification.Page ?? 1, pagedSpecification.PageSize ?? totalItems, totalItems);
        }

        /// <summary>
        /// Converts a collection of items to a PagedListDTO based on the provided sorted and paged specification.
        /// </summary>
        /// <typeparam name="TEntity">The type of the entity.</typeparam>
        /// <typeparam name="TDTO">The type of the DTO.</typeparam>
        /// <param name="items">The collection of items.</param>
        /// <param name="sortedAndPagedSpecification">The sorted and paged specification.</param>
        /// <param name="totalItems">The total number of items.</param>
        /// <returns>A PagedListDTO containing the paged items.</returns>

        public static PagedListDTO<TDTO> ToPagedListDTO<TEntity, TDTO>(IEnumerable<TDTO> items, SortedAndPagedSpecification<TEntity> sortedAndPagedSpecification, int totalItems)
            where TEntity : class
        {
            return CreatePagedListDTO(items, sortedAndPagedSpecification.Page ?? 1, sortedAndPagedSpecification.PageSize ?? totalItems, totalItems);
        }

        /// <summary>
        /// Creates a PagedListDTO from the provided items, page, page size, and total items.
        /// </summary>
        /// <typeparam name="T">The type of the items.</typeparam>
        /// <param name="items">The collection of items.</param>
        /// <param name="page">The current page number.</param>
        /// <param name="pageSize">The size of the page.</param>
        /// <param name="totalItems">The total number of items.</param>
        /// <returns>A PagedListDTO containing the paged items.</returns>
        public static PagedListDTO<T> CreatePagedListDTO<T>(IEnumerable<T> items, int page, int pageSize, int totalItems)
        {
            return new PagedListDTO<T>(
                page,
                (int)Math.Ceiling((double)totalItems / pageSize),
                pageSize,
                totalItems,
                items.ToArray()
            );
        }
    }

}
