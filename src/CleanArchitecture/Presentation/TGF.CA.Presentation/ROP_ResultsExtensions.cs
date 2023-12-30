using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using System.Net;
using System.Text.Json;
using TGF.Common.ROP.Errors;
using TGF.Common.ROP.HttpResult;

namespace TGF.CA.Presentation
{
    /// <summary>
    /// Static class to support conversion of <see cref="IHttpResult{T}"/> into <see cref="IResult"/>.
    /// </summary>
    public static class ROP_ResultsExtensions
    {
        private static JsonSerializerOptions? _cachedJsonOptions;

        /// <summary>
        /// Gets a new instance of an <see cref="IResult"/> created from the provided <paramref name="aHttpResult"/>.
        /// </summary>
        /// <typeparam name="T">Type of the Value propery from <see cref="IHttpResult{T}"/>.</typeparam>
        /// <param name="aHttpResult">An instance of <see cref="IHttpResult{T}"/>.</param>
        /// <returnsawaitabl    e <see cref="Task{IResult}"/>.></returns>
        public static IResult ToIResult<T>(this IHttpResult<T> aHttpResult)
            => aHttpResult.IsSuccess
                   ? aHttpResult.Value.ToResponseResult(aHttpResult.StatusCode)
                   : aHttpResult.ToProblemDetailResponseResult();

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

            public async Task ExecuteAsync(HttpContext httpContext)
            {
                httpContext.Response.ContentType = "application/json";
                httpContext.Response.StatusCode = _httpStatusCode;

                _cachedJsonOptions ??= httpContext.RequestServices.GetService<IOptions<JsonOptions>>()?.Value?.JsonSerializerOptions;

                var lJson = System.Text.Json.JsonSerializer.Serialize(_resultValue, _cachedJsonOptions);
                await httpContext.Response.WriteAsync(lJson);
            }
        }

        #region ProblemDetail building

        /// <summary>
        /// Converts an <see cref="IHttpResult{T}"/> to a problem detail response result. The methos assumes the <see cref="IHttpResult{T}"/> is not a success result.
        /// </summary>
        /// <typeparam name="T">The type of the value contained in the <see cref="IHttpResult{T}"/>.</typeparam>
        /// <param name="aHttpResult">The <see cref="IHttpResult{T}"/> to be converted.</param>
        /// <returns>
        /// A problem detail response result containing the details of the validation errors if they exist, 
        /// or a problem detail containing the first error from the error list, along with the associated HTTP status code.
        /// </returns>
        private static IResult ToProblemDetailResponseResult<T>(this IHttpResult<T> aHttpResult)
                => aHttpResult.HasValidationErrors()
                   ? Results.ValidationProblem(aHttpResult.ErrorList.ConvertToErrorDictionary())
                   : Results.Problem(title: aHttpResult.ErrorList.First().Code,
                                     detail: aHttpResult.ErrorList.GetErrorListAsString(),
                                     statusCode: (int)aHttpResult.StatusCode);

        /// <summary>
        /// Converts a list of errors to a dictionary where the key is the error code, and the value is an array of error messages.
        /// </summary>
        /// <param name="aErrorList">The list of errors to convert.</param>
        /// <returns>A dictionary containing error codes as keys and error messages as values.</returns>
        private static Dictionary<string, string[]> ConvertToErrorDictionary(this IEnumerable<IError> aErrorList)
        {
            var lErrorDictionary = new Dictionary<string, List<string>>();
            foreach (var lError in aErrorList)
            {
                if (!lErrorDictionary.TryGetValue(lError.Code, out var lErrorMessages))
                {
                    lErrorMessages = new List<string>();
                    lErrorDictionary[lError.Code] = lErrorMessages;
                }
                lErrorMessages.Add(lError.Message);
            }

            return lErrorDictionary.ToDictionary(kvp => kvp.Key, kvp => kvp.Value.ToArray());
        }
        #endregion

    }
}