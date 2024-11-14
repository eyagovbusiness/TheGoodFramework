using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace TGF.CA.Application.DTOs
{
        /// <summary>
        /// Contains all filtering options of a list endpoint with pagination.
        /// </summary>
        /// <param name="Page">The page number to get.</param>
        /// <param name="PageSize">The page size to use.</param>
        /// <param name="SortBy">The name of the property to sort by the results.</param>
        /// <param name="Filters">The filters to be applied.</param>
        public record ListPaginationFiltersDTO(int Page, int PageSize, string? SortBy, string? Filters);
}
