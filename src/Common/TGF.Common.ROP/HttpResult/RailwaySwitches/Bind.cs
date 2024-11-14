namespace TGF.Common.ROP.HttpResult.RailwaySwitches {
    /// <summary>
    /// Provides extension methods to Bind (or chain) multiple <see cref="IHttpResult{T}"/> instances.
    /// </summary>
    public static class BindSwitchExtensions {

        /// <summary>
        /// Asynchronously binds a given <see cref="IHttpResult{T1}"/> to another resulting <see cref="IHttpResult{T2}"/>, chaining their outcomes.
        /// </summary>
        /// <typeparam name="T1">Original result type.</typeparam>
        /// <typeparam name="T2">Target result type.</typeparam>
        /// <param name="aThisResult">The task returning the initial result.</param>
        /// <param name="aNextResult">A function producing the next result based on the value of the first result.</param>
        /// <returns>A task yielding the resulting <see cref="IHttpResult{T2}"/>.</returns>
        /// <remarks>Async to Async binding.</remarks>
        public static async Task<IHttpResult<T2>> Bind<T1, T2>(this Task<IHttpResult<T1>> aThisResult, Func<T1, Task<IHttpResult<T2>>> aNextResult) {
            var lThisResult = await aThisResult;
            return lThisResult.IsSuccess
                ? await aNextResult(lThisResult.Value)
                : Result.Result.Failure<T2>(lThisResult.ErrorList, lThisResult.StatusCode);

        }

        /// <summary>
        /// Asynchronously binds a given <see cref="IHttpResult{T1}"/> to another resulting <see cref="IHttpResult{T2}"/>, chaining their outcomes.
        /// </summary>
        /// <typeparam name="T1">Original result type.</typeparam>
        /// <typeparam name="T2">Target result type.</typeparam>
        /// <param name="aThisResult">The task returning the initial result.</param>
        /// <param name="aNextResult">A function producing the next result based on the value of the first result.</param>
        /// <returns>A task yielding the resulting <see cref="IHttpResult{T2}"/>.</returns>
        /// <remarks>Async to Sync binding, IDEALLY IT SHOULD NEVER BE USED. Although the result is wrapped in a task due to the async nature of the first result.</remarks>
        public static async Task<IHttpResult<T2>> Bind<T1, T2>(this Task<IHttpResult<T1>> aThisResult, Func<T1, IHttpResult<T2>> aNextResult) {
            var lThisResult = await aThisResult;
            return lThisResult.IsSuccess
                ? aNextResult(lThisResult.Value)
                : Result.Result.Failure<T2>(lThisResult.ErrorList, lThisResult.StatusCode);

        }

        /// <summary>
        /// Binds a given <see cref="IHttpResult{T1}"/> to another resulting <see cref="IHttpResult{T2}"/>, chaining their outcomes.
        /// </summary>
        /// <typeparam name="T1">Original result type.</typeparam>
        /// <typeparam name="T2">Target result type.</typeparam>
        /// <param name="aThisResult">The initial result.</param>
        /// <param name="aNextResult">A function producing the next result based on the value of the first result.</param>
        /// <returns>A task yielding the resulting <see cref="IHttpResult{T2}"/>.</returns>
        /// <remarks>Sync to Async binding.</remarks>
        public static async Task<IHttpResult<T2>> Bind<T1, T2>(this IHttpResult<T1> aThisResult, Func<T1, Task<IHttpResult<T2>>> aNextResult)
        => aThisResult.IsSuccess
            ? await aNextResult(aThisResult.Value)
            : Result.Result.Failure<T2>(aThisResult.ErrorList, aThisResult.StatusCode);

        /// <summary>
        /// Binds a given <see cref="IHttpResult{T1}"/> to another resulting <see cref="IHttpResult{T2}"/>, chaining their outcomes.
        /// </summary>
        /// <typeparam name="T1">Original result type.</typeparam>
        /// <typeparam name="T2">Target result type.</typeparam>
        /// <param name="aThisResult">The initial result.</param>
        /// <param name="aNextResult">A function producing the next result based on the value of the first result.</param>
        /// <returns>The resulting <see cref="IHttpResult{T2}"/>.</returns>
        /// <remarks>Sync to Sync binding.</remarks>
        public static IHttpResult<T2> Bind<T1, T2>(this IHttpResult<T1> aThisResult, Func<T1, IHttpResult<T2>> aNextResult)
        => aThisResult.IsSuccess
            ? aNextResult(aThisResult.Value)
            : Result.Result.Failure<T2>(aThisResult.ErrorList, aThisResult.StatusCode);

    }
}
