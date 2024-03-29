using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace TGF.CA.Application.DTOs
{
    /// <summary>
    /// Genetric DTO for paginated lists.
    /// </summary>
    /// <typeparam name="T">Type of the items in the paginated list.</typeparam>
    /// <param name="CurrentPage">The current page</param>
    /// <param name="TotalPages">The total number of pages.</param>
    /// <param name="PageSize">The page size</param>
    /// <param name="TotalCount">The total count of items in this list.</param>
    /// <param name="List">The list of paginated items.</param>
    public record PaginatedListDTO<T>(int CurrentPage, int TotalPages, int PageSize, int TotalCount, T[] List);
}
