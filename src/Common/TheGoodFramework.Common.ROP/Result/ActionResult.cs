using Microsoft.AspNetCore.Mvc;
using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;
using System.Net;
using System.Text;
using System.Threading.Tasks;
using TGF.Common.ROP.Errors;
using TGF.Common.ROP.HttpResult;

namespace TGF.Common.ROP.Result
{
    public static class ActionResultExtensions
    {
        private class ResultWithStatusCode<T> : ObjectResult
        {
            public ResultWithStatusCode(T aContext, HttpStatusCode aStatusCode)
                : base(aContext)
            {
                base.StatusCode = (int)aStatusCode;
            }
        }

        public static IActionResult ToActionResult<T>(this IHttpResult<T> aHttpResult)
        {
            return aHttpResult.ToHttpStatusCode(aHttpResult.StatusCode);
        }

        public static async Task<IActionResult> ToActionResult<T>(this Task<IHttpResult<T>> aHttpResult)
        {
            return (await aHttpResult).ToActionResult();
        }

        private static IActionResult ToHttpStatusCode<T>(this T aHttpResult, HttpStatusCode aStatusCode)
        {
            return new ResultWithStatusCode<T>(aHttpResult, aStatusCode);
        }

    }
}
