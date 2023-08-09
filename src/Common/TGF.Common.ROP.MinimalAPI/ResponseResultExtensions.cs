using Microsoft.AspNetCore.Http;
using Newtonsoft.Json;
using System.Net;
using TGF.Common.ROP.Errors;
using TGF.Common.ROP.HttpResult;

namespace TGF.Common.ROP.MinimalAPI
{
    /// <summary>
    /// Static class to support conversion of <see cref="IHttpResult{T}"/> into <see cref="IResult"/>.
    /// </summary>
    public static class ResultsExtensions
    {
        /// <summary>
        /// Gets a new instance of an <see cref="IResult"/> created from the provided <paramref name="aHttpResult"/>.
        /// </summary>
        /// <typeparam name="T">Type of the Value propery from <see cref="IHttpResult{T}"/>.</typeparam>
        /// <param name="aHttpResult">An instance of <see cref="IHttpResult{T}"/>.</param>
        /// <returnsawaitabl    e <see cref="Task{IResult}"/>.></returns>
        public static IResult ToIResult<T>(this IHttpResult<T> aHttpResult)
            => aHttpResult.IsSuccess
                   ? aHttpResult.Value.ToResponseResult(aHttpResult.StatusCode)
                   : Results.Problem(title: aHttpResult.ErrorList.First().Code,
                                     detail: aHttpResult.ErrorList.GetErrorListAsString(),
                                     statusCode: (int)aHttpResult.StatusCode);

        /// <summary>
        /// Gets a <see cref="Task"/> that returns a new instance of an <see cref="IResult"/> created from the provided <paramref name="aHttpResult"/>.
        /// </summary>
        /// <typeparam name="T">Type of the Value propery from <see cref="IHttpResult{T}"/>.</typeparam>
        /// <param name="aHttpResult">An instance of <see cref="IHttpResult{T}"/>.</param>
        /// <returns>awaitable <see cref="Task{IResult}"/>.</return
        public static async Task<IResult> ToIResult<T>(this Task<IHttpResult<T>> aHttpResult)
            => (await aHttpResult).ToIResult();

        /// <summary>
        /// Creates and returns a new instance of <see cref="ResponseResult{T}"/> from the givn <typeparamref name="T"/> as <see cref="IResult"/>.
        /// </summary>
        /// <typeparam name="T">Type of the source object that will be used to create the resulting <see cref="ResponseResult{T}"/>.</typeparam>
        /// <param name="aObjectResultContextValue">Instance of the source object that will be used to create the resulting <see cref="ResponseResult{T}"/> from <see cref="IResult".</param>
        /// <param name="aObjectResultStatusCode">HTTP StatusCode to be set in the returning <see cref="ResponseResult{T}".</param>
        /// <returns><see cref="IResult"/></returns>
        private static IResult ToResponseResult<T>(this T aObjectResultContextValue, HttpStatusCode aObjectResultStatusCode)
            => new ResponseResult<T>(aObjectResultContextValue, aObjectResultStatusCode);

        internal class ResponseResult<T> : IResult
        {
            private readonly T _resultValue;
            private readonly int _httpStatusCode;

            public ResponseResult(T aResultValue, HttpStatusCode aHttpStatusCode)
            {
                _resultValue = aResultValue;
                _httpStatusCode = (int)aHttpStatusCode;
            }

            public Task ExecuteAsync(HttpContext httpContext)
            {
                httpContext.Response.ContentType = "application/json";
                httpContext.Response.StatusCode = _httpStatusCode;

                // Serialize the JSON data and write it to the response
                var json = JsonConvert.SerializeObject(_resultValue);
                return httpContext.Response.WriteAsync(json);
            }

        }

        #region To-DO: Add to IHttpError a property to specify if it is a validation error, and handle if the IResult will be a Results.ValidationProblem() ir Results.Problem() based on that
        //private static IResult ToProblemDetailResponseResult<T>(this IEnumerable<HttpError> aHttpErrorList, HttpStatusCode aProblemStatusCode)
        //        => aHttpErrorList.First().IsValidationError
        //           ? Results.ValidationProblem(aHttpErrorList.ConvertToErrorDictionary())
        //           : Results.Problem(title: aHttpErrorList.First().Code,
        //                             detail: aHttpErrorList.GetErrorListAsString(),
        //                             statusCode: (int)aHttpResult.StatusCode);
        //public static Dictionary<string, string> ConvertToErrorDictionary(this IEnumerable<HttpError> httpErrors)
        //{
        //    var errorDictionary = new Dictionary<string, string>();

        //    foreach (var httpError in httpErrors)
        //    {
        //        string errorCode = httpError.Error.Code;
        //        string errorMessage = httpError.Error.Message;

        //        if (!errorDictionary.ContainsKey(errorCode))
        //        {
        //            errorDictionary.Add(errorCode, errorMessage);
        //        }
        //    }

        //    return errorDictionary;
        //}
        #endregion

    }
}
