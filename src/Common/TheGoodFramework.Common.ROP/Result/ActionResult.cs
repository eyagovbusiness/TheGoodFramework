using Microsoft.AspNetCore.Mvc;
using System.Net;
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
        public static async Task<IActionResult> ToActionResult<T>(this Task<IResult<T>> aHttpResult)
        {
            return (await aHttpResult).TryHttpResultParse().ToActionResult();
        }

        private static IActionResult ToHttpStatusCode<T>(this T aHttpResult, HttpStatusCode aStatusCode)
        {
            return new ResultWithStatusCode<T>(aHttpResult, aStatusCode);
        }

    }
}
