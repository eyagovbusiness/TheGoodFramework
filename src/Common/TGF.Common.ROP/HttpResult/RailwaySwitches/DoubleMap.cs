
namespace TGF.Common.ROP.HttpResult
{
    /// <summary>
    /// Provides extension methods to Map both(happy and not-happy path for <see cref="IHttpResult{T}"/> instances.
    /// </summary>
    public static partial class DoubleMapSwitchExtensions
    {

        /// <summary>
        /// Maps both the successful and failure paths of a railway-oriented <see cref="IHttpResult{T1}"/> using provided mapping functions.
        /// </summary>
        /// <typeparam name="T1">The type of the original result value.</typeparam>
        /// <typeparam name="T2">The type of the mapped result value.</typeparam>
        /// <param name="aThisResult">The task producing the <see cref="IHttpResult{T1}"/> to map.</param>
        /// <param name="aMapSuccessFunction">A function to map from <typeparamref name="T1"/> to <typeparamref name="T2"/> in case of a successful result.</param>
        /// <param name="aMapFailureFunction">A function to map from <typeparamref name="T1"/> to <typeparamref name="T2"/> in case of a failure.</param>
        /// <returns>
        /// A task producing an <see cref="IHttpResult{T2}"/>, where the value is determined by either the success or failure mapping function, as appropriate.
        /// </returns>
        /// <remarks>Async to Async.</remarks>
        public static async Task<IHttpResult<T2>> DoubleMap<T1, T2>(this Task<IHttpResult<T1>> aThisResult, Func<T1, Task<T2>> aMapSuccessFunction, Func<T1, Task<T2>> aMapFailureFunction)
        {
            var lThisResult = await aThisResult;
            if (lThisResult.IsSuccess)
                return Result.Result.Success(await aMapSuccessFunction(lThisResult.Value), lThisResult.StatusCode);

            await aMapFailureFunction(lThisResult.Value);
            return Result.Result.Failure<T2>(lThisResult.ErrorList, lThisResult.StatusCode);

        }

        /// <summary>
        /// Maps both the successful and failure paths of a railway-oriented <see cref="IHttpResult{T1}"/> using the provided mapping functions.
        /// </summary>
        /// <typeparam name="T1">The type of the original result value.</typeparam>
        /// <typeparam name="T2">The type of the mapped result value.</typeparam>
        /// <param name="aThisResult">The task producing the <see cref="IHttpResult{T1}"/> to map.</param>
        /// <param name="aMapSuccessFunction">A function to map from <typeparamref name="T1"/> to <typeparamref name="T2"/> in case of a successful result.</param>
        /// <param name="aMapFailureFunction">A function to map from <typeparamref name="T1"/> to <typeparamref name="T2"/> in case of a failure, without wrapping in a task.</param>
        /// <returns>
        /// A task producing an <see cref="IHttpResult{T2}"/>, where the value is determined by either the success or failure mapping function, as appropriate.
        /// </returns>
        /// <remarks>Async to Sync, IDEALLY IT SHOULD NEVER BE USED.</remarks>
        public static async Task<IHttpResult<T2>> DoubleMap<T1, T2>(this Task<IHttpResult<T1>> aThisResult, Func<T1, Task<T2>> aMapSuccessFunction, Func<T1, T2> aMapFailureFunction)
        {
            var lThisResult = await aThisResult;
            if (lThisResult.IsSuccess)
                return Result.Result.Success(await aMapSuccessFunction(lThisResult.Value), lThisResult.StatusCode);

            aMapFailureFunction(lThisResult.Value);
            return Result.Result.Failure<T2>(lThisResult.ErrorList, lThisResult.StatusCode);

        }

        //To-Do:Implement more variants as needed.

    }
}
