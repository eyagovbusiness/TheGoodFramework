using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace TGF.Common.ROP.Errors
{
    public static class ErrorExtensions
    {
        public static string GetHttpErrorListAsString(this IEnumerable<IHttpError> aHttpErrorList)
        {
            return string.Join(", ", aHttpErrorList);
        }
        public static string GetErrorListAsString(this IEnumerable<IError> aErrorList)
        {
            return string.Join(", ", aErrorList);
        }
    }
}