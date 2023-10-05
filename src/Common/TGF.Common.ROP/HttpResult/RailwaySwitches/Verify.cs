using TGF.Common.ROP.Errors;

namespace TGF.Common.ROP.HttpResult
{
    /// <summary>
    /// Provides extension methods for <see cref="IHttpResult{T}"/> to verify results and conditionally modify the path with the provided <see cref="IHttpError"/> based on verification logic.
    /// </summary>
    public static class VerifySwitchExtensions
    {
        /// <summary>
        /// Asynchronously verifies the result using the provided function. If the result does not meet the verification criteria, 
        /// it returns a failure with the specified error. Otherwise, it returns the original result.
        /// </summary>
        /// <typeparam name="T">The type of the result value.</typeparam>
        /// <param name="aThisResult">The task producing the <see cref="IHttpResult{T}"/> to verify.</param>
        /// <param name="aVerifyFunction">The asynchronous function to use for verification.</param>
        /// <param name="aHttpError">The error to return in case of verification 
        /// <remarks>From Async with Async verify function.</remarks>
        public static async Task<IHttpResult<T>> Verify<T>(this Task<IHttpResult<T>> aThisResult, Func<T, Task<bool>> aVerifyFunction, IHttpError aHttpError)
        {
            var lThisResult = await aThisResult;
            return lThisResult.IsSuccess && !(await aVerifyFunction(lThisResult.Value))
                ? Result.Result.Failure<T>(aHttpError)
                : lThisResult;
        }

        /// <summary>
        /// Verifies the result using the provided function. If the result does not meet the verification criteria, 
        /// it returns a failure with the specified error. Otherwise, it returns the original result.
        /// </summary>
        /// <typeparam name="T">The type of the result value.</typeparam>
        /// <param name="aThisResult">The task producing the <see cref="IHttpResult{T}"/> to verify.</param>
        /// <param name="aVerifyFunction">The function to use for verification.</param>
        /// <param name="aHttpError">The error to return in case of verification failure.</param>
        /// <returns>A task producing either the original result or a failure, depending on the verification.</returns>
        /// <remarks>From Async with Sync verify function, IDEALLY IT SHOULD NEVER BE USED.</remarks>
        public static async Task<IHttpResult<T>> Verify<T>(this Task<IHttpResult<T>> aThisResult, Func<T, bool> aVerifyFunction, IHttpError aHttpError)
        {
            var lThisResult = await aThisResult;
            return lThisResult.IsSuccess && !aVerifyFunction(lThisResult.Value)
                ? Result.Result.Failure<T>(aHttpError)
                : lThisResult;
        }

        /// <summary>
        /// Asynchronously verifies the result using the provided function. If the result does not meet the verification criteria, 
        /// it returns a failure with the specified error. Otherwise, it returns the original result.
        /// </summary>
        /// <typeparam name="T">The type of the result value.</typeparam>
        /// <param name="aThisResult">The <see cref="IHttpResult{T}"/> to verify.</param>
        /// <param name="aVerifyFunction">The asynchronous function to use for verification.</param>
        /// <param name="aHttpError">The error to return in case of verification failure.</param>
        /// <returns>Either the original result or a failure, depending on the verification.</returns>
        /// <remarks>From Sync with Async verify function.</remarks>
        public static async Task<IHttpResult<T>> Verify<T>(this IHttpResult<T> aThisResult, Func<T, Task<bool>> aVerifyFunction, IHttpError aHttpError)
        => aThisResult.IsSuccess && !(await aVerifyFunction(aThisResult.Value))
            ? Result.Result.Failure<T>(aHttpError)
            : aThisResult;

        /// <summary>
        /// Verifies the result using the provided function. If the result does not meet the verification criteria, 
        /// it returns a failure with the specified error. Otherwise, it returns the original result.
        /// </summary>
        /// <typeparam name="T">The type of the result value.</typeparam>
        /// <param name="aThisResult">The <see cref="IHttpResult{T}"/> to verify.</param>
        /// <param name="aVerifyFunction">The function to use for verification.</param>
        /// <param name="aHttpError">The error to return in case of verification failure.</param>
        /// <returns>Either the original result or a failure, depending on the verification.</returns>
        /// <remarks>From Sync with Sync verify function.</remarks>
        public static IHttpResult<T> Verify<T>(this IHttpResult<T> aThisResult, Func<T, bool> aVerifyFunction, IHttpError aHttpError)
        => aThisResult.IsSuccess && !aVerifyFunction(aThisResult.Value)
            ? Result.Result.Failure<T>(aHttpError)
            : aThisResult;
    }
}
