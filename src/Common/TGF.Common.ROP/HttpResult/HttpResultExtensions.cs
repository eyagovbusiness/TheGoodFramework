using TGF.Common.ROP.Errors;

namespace TGF.Common.ROP.HttpResult
{
    public static class HttpResultExtensions
    {
        public static bool HasValidationErrors<T>(this IHttpResult<T> aHttpResult)
            => aHttpResult.ErrorList.Any(error => error is ValidationError);
    }
}
