using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace TGF.CA.Application.DTOs
{
    public record PaginatedListDTO<T>(int CurrentPage, int TotalPages, int PageSize, int TotalCount, T[] List);
}
