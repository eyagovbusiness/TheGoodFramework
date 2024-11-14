namespace TGF.Common.ROP.HttpResult.RailwaySwitches {
    /// <summary>
    /// Provides extension methods for <see cref="IHttpResult{T}"/> to execute side-effect actions on successful results without modifying the original result.
    /// </summary>
    public static class TapSwitchExtensions {

        /// <summary>
        /// Asynchronously executes the given function on a successful result and then returns the original result.
        /// </summary>
        /// <typeparam name="T">The type of the result value.</typeparam>
        /// <param name="aThisResult">The task producing the <see cref="IHttpResult{T}"/> to act upon.</param>
        /// <param name="aDeadEndAsyncFunc">The asynchronous function to execute if the result is successful.</param>
        /// <returns>A task producing the original <see cref="IHttpResult{T}"/>.</returns>
        /// <remarks>From Async with Async DeadEndFunction.</remarks>
        public static async Task<IHttpResult<T>> Tap<T>(this Task<IHttpResult<T>> aThisResult, Func<T, Task> aDeadEndAsyncFunc) {
            var lThisResult = await aThisResult;
            if (lThisResult.IsSuccess)
                await aDeadEndAsyncFunc(lThisResult.Value);

            return lThisResult;

        }

        /// <summary>
        /// Asynchronously executes the given action on a successful result and then returns the original result.
        /// </summary>
        /// <typeparam name="T">The type of the result value.</typeparam>
        /// <param name="aThisResult">The task producing the <see cref="IHttpResult{T}"/> to act upon.</param>
        /// <param name="aDeadEndAction">The action to execute if the result is successful.</param>
        /// <returns>A task producing the original <see cref="IHttpResult{T}"/>.</returns>
        /// <remarks>From Async with Sync DeadEndFunction, IDEALLY IT SHOULD NEVER BE USED.</remarks>
        public static async Task<IHttpResult<T>> Tap<T>(this Task<IHttpResult<T>> aThisResult, Action<T> aDeadEndAction) {
            var lThisResult = await aThisResult;
            if (lThisResult.IsSuccess)
                aDeadEndAction(lThisResult.Value);

            return lThisResult;

        }

        /// <summary>
        /// Executes the given asynchronous function on a successful result and then returns the original result.
        /// </summary>
        /// <typeparam name="T">The type of the result value.</typeparam>
        /// <param name="aThisResult">The <see cref="IHttpResult{T}"/> to act upon.</param>
        /// <param name="aDeadEndAsyncFunc">The asynchronous function to execute if the result is successful.</param>
        /// <returns>The original <see cref="IHttpResult{T}"/>.</returns>
        /// <remarks>From Sync with Async DeadEndFunction.</remarks>
        public static async Task<IHttpResult<T>> Tap<T>(this IHttpResult<T> aThisResult, Func<T, Task> aDeadEndAsyncFunc) {
            if (aThisResult.IsSuccess)
                await aDeadEndAsyncFunc(aThisResult.Value);
            return aThisResult;
        }

        /// <summary>
        /// Executes the given action on a successful result and then returns the original result.
        /// </summary>
        /// <typeparam name="T">The type of the result value.</typeparam>
        /// <param name="aThisResult">The <see cref="IHttpResult{T}"/> to act upon.</param>
        /// <param name="aDeadEndAction">The action to execute if the result is successful.</param>
        /// <returns>The original <see cref="IHttpResult{T}"/>.</returns>
        /// <remarks>From Sync with Sync DeadEndFunction.</remarks>
        public static IHttpResult<T> Tap<T>(this IHttpResult<T> aThisResult, Action<T> aDeadEndAction) {
            if (aThisResult.IsSuccess)
                aDeadEndAction(aThisResult.Value);
            return aThisResult;
        }

    }
}
