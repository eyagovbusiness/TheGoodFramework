using Microsoft.AspNetCore.Mvc;
using System.Net;
using TGF.Common.ROP.Errors;
using TGF.Common.ROP.HttpResult;

namespace TGF.Common.ROP.Result
{
    /// <summary>
    /// Static class to support conversion of <see cref="IHttpResult{T}"/> into <see cref="IActionResult"/>.
    /// </summary>
    public static class ActionResultExtensions
    {

        /// <summary>
        /// Specialization of <see cref="ObjectResult"/> with the StatusCode set from the given status code from an <see cref="IHttpResult{T}"/> instance.
        /// </summary>
        /// <typeparam name="T">Type of the context, expected <see cref="IResult{T}"/>.</typeparam>
        private class ResultWithStatusCode<T> : ObjectResult
        {
            public ResultWithStatusCode(T aContext, HttpStatusCode aStatusCode)
                : base(aContext)
                => base.StatusCode = (int)aStatusCode;
        }

        /// <summary>
        /// Gets a <see cref="Task"/> that returns a new instance of an <see cref="IActionResult"/> created from the provided <paramref name="aHttpResult"/>.
        /// </summary>
        /// <typeparam name="T">Type of the Value propery from <see cref="IResult{T}"/>.</typeparam>
        /// <param name="aHttpResult">An instance of <see cref="IHttpResult{T}"/> as <see cref="IResult{T}"/>.</param>
        /// <returns>awaitable <see cref="Task{IActionResult}"/>.</returns>
        public static async Task<IActionResult> ToActionResult<T>(this Task<IResult<T>> aHttpResult)
            => (await aHttpResult).TryHttpResultParse().ToActionResult();

        /// <summary>
        /// Gets a new instance of an <see cref="IActionResult"/> created from the provided <paramref name="aHttpResult"/>.
        /// </summary>
        /// <typeparam name="T">Type of the Value propery from <see cref="IHttpResult{T}"/>.</typeparam>
        /// <param name="aHttpResult">An instance of <see cref="IHttpResult{T}"/>.</param>
        /// <returnsawaitable <see cref="Task{IActionResult}"/>.></returns>
        public static IActionResult ToActionResult<T>(this IHttpResult<T> aHttpResult)
            => aHttpResult.IsSuccess
                   ? aHttpResult.Value.ToResultWithStatusCode(aHttpResult.StatusCode)
                   : aHttpResult.ErrorList.GetErrorListAsString().ToResultWithStatusCode(aHttpResult.StatusCode);

        /// <summary>
        /// Creates and returns a new instance of <see cref="ResultWithStatusCode{T}"/> from the givn <typeparamref name="T"/> as <see cref="IActionResult"/>.
        /// </summary>
        /// <typeparam name="T">Type of the source object that will be used to create the resulting <see cref="ResultWithStatusCode{T}"/>.</typeparam>
        /// <param name="aObjectResultContextValue">Instance of the source object that will be used to create the resulting <see cref="ResultWithStatusCode{T}"/> from <see cref="ObjectResult".</param>
        /// <param name="aObjectResultStatusCode">HTTP StatusCode to be set in the returning <see cref="ResultWithStatusCode{T}".</param>
        /// <returns><see cref="IActionResult"/></returns>
        private static IActionResult ToResultWithStatusCode<T>(this T aObjectResultContextValue, HttpStatusCode aObjectResultStatusCode)
            => new ResultWithStatusCode<T>(aObjectResultContextValue, aObjectResultStatusCode);

        /// <summary>
        /// Gets a <see cref="Task"/> that returns a new instance of an <see cref="IActionResult"/> created from the provided <paramref name="aHttpResult"/>.
        /// </summary>
        /// <typeparam name="T">Type of the Value propery from <see cref="IHttpResult{T}"/>.</typeparam>
        /// <param name="aHttpResult">An instance of <see cref="IHttpResult{T}"/>.</param>
        /// <returns>awaitable <see cref="Task{IActionResult}"/>.</return
        public static async Task<IActionResult> ToActionResult<T>(this Task<IHttpResult<T>> aHttpResult)
            => (await aHttpResult).ToActionResult();

    }
}
