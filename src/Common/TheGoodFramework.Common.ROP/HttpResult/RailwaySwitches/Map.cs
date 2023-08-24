
namespace TGF.Common.ROP.HttpResult
{
    /// <summary>
    /// Provides extension methods to Map the happy path of <see cref="IHttpResult{T}"/> instances.
    /// </summary>
    public static class MapSwitchExtensions
    {

        /// <summary>
        /// Maps the value of a successful <see cref="IHttpResult{T1}"/> to another value of type <typeparamref name="T2"/> using the provided asynchronous mapping function.
        /// </summary>
        /// <typeparam name="T1">The original value type.</typeparam>
        /// <typeparam name="T2">The target mapped value type.</typeparam>
        /// <param name="aThisResult">The task producing the <see cref="IHttpResult{T1}"/> to map.</param>
        /// <param name="aMapSuccessFunction">An asynchronous function to map from <typeparamref name="T1"/> to <typeparamref name="T2"/> in case of a successful result.</param>
        /// <returns>
        /// A task producing an <see cref="IHttpResult{T2}"/>, containing the mapped value if the original result was successful, or the original error(s) and status code otherwise.
        /// </returns>
        /// <remarks>From Async with Async mapping.</remarks>
        public static async Task<IHttpResult<T2>> Map<T1, T2>(this Task<IHttpResult<T1>> aThisResult, Func<T1, Task<T2>> aMapSuccessFunction)
        {
            var lThisResult = await aThisResult;
            return lThisResult.IsSuccess
                ? Result.Result.Success(await aMapSuccessFunction(lThisResult.Value), lThisResult.StatusCode)
                : Result.Result.Failure<T2>(lThisResult.ErrorList, lThisResult.StatusCode);

        }

        /// <summary>
        /// Maps the value of a successful <see cref="IHttpResult{T1}"/> to another value of type <typeparamref name="T2"/> using the provided synchronous mapping function.
        /// </summary>
        /// <typeparam name="T1">The original value type.</typeparam>
        /// <typeparam name="T2">The target mapped value type.</typeparam>
        /// <param name="aThisResult">The task producing the <see cref="IHttpResult{T1}"/> to map.</param>
        /// <param name="aMapSuccessFunction">A function to map from <typeparamref name="T1"/> to <typeparamref name="T2"/> in case of a successful result.</param>
        /// <returns>
        /// A task producing an <see cref="IHttpResult{T2}"/>, containing the mapped value if the original result was successful, or the original error(s) and status code otherwise.
        /// </returns>
        /// <remarks>From Async with Sync mapping, although the result is wrapped in a task due to the async nature of the first result.</remarks>
        public static async Task<IHttpResult<T2>> Map<T1, T2>(this Task<IHttpResult<T1>> aThisResult, Func<T1, T2> aMapSuccessFunction)
        {
            var lThisResult = await aThisResult;
            return lThisResult.IsSuccess
                ? Result.Result.Success(aMapSuccessFunction(lThisResult.Value), lThisResult.StatusCode)
                : Result.Result.Failure<T2>(lThisResult.ErrorList, lThisResult.StatusCode);

        }

        /// <summary>
        /// Maps the value of a successful <see cref="IHttpResult{T1}"/> to another value of type <typeparamref name="T2"/> using the provided asynchronous mapping function.
        /// </summary>
        /// <typeparam name="T1">The original value type.</typeparam>
        /// <typeparam name="T2">The target mapped value type.</typeparam>
        /// <param name="aThisResult">The <see cref="IHttpResult{T1}"/> to map.</param>
        /// <param name="aMapSuccessFunction">An asynchronous function to map from <typeparamref name="T1"/> to <typeparamref name="T2"/> in case of a successful result.</param>
        /// <returns>
        /// An <see cref="IHttpResult{T2}"/> containing the mapped value if the original result was successful, or the original error(s) and status code otherwise.
        /// </returns>
        /// <remarks>From Sync with Async mapping.</remarks>
        public static async Task<IHttpResult<T2>> Map<T1, T2>(this IHttpResult<T1> aThisResult, Func<T1, Task<T2>> aMapSuccessFunction)
        {
            return aThisResult.IsSuccess
                ? Result.Result.Success(await aMapSuccessFunction(aThisResult.Value), aThisResult.StatusCode)
                : Result.Result.Failure<T2>(aThisResult.ErrorList, aThisResult.StatusCode);

        }

        /// <summary>
        /// Maps the value of a successful <see cref="IHttpResult{T1}"/> to another value of type <typeparamref name="T2"/> using the provided synchronous mapping function.
        /// </summary>
        /// <typeparam name="T1">The original value type.</typeparam>
        /// <typeparam name="T2">The target mapped value type.</typeparam>
        /// <param name="aThisResult">The <see cref="IHttpResult{T1}"/> to map.</param>
        /// <param name="aMapSuccessFunction">A function to map from <typeparamref name="T1"/> to <typeparamref name="T2"/> in case of a successful result.</param>
        /// <returns>
        /// An <see cref="IHttpResult{T2}"/> containing the mapped value if the original result was successful, or the original error(s) and status code otherwise.
        /// </returns>
        /// <remarks>From Sync with Sync mapping.</remarks>
        public static IHttpResult<T2> Map<T1, T2>(this IHttpResult<T1> aThisResult, Func<T1, T2> aMapSuccessFunction)
        {
            return aThisResult.IsSuccess
                ? Result.Result.Success(aMapSuccessFunction(aThisResult.Value), aThisResult.StatusCode)
                : Result.Result.Failure<T2>(aThisResult.ErrorList, aThisResult.StatusCode);

        }

    }
}
