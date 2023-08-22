using TGF.Common.ROP.Errors;

namespace TGF.Common.ROP.HttpResult
{
    /// <summary>
    /// Static class to register operations between Results like binding Results, mapping different Results or executing certain actions after the Result is generated.
    /// </summary>
    public static partial class RailwaySwitchExtensions
    {

        /// <summary>
        /// Binds a second Result after this one (concats railways and the respective not happy paths).
        /// </summary>
        /// <typeparam name="T1">Type of the Value property of this Result.</typeparam>
        /// <typeparam name="T2">Type of the Value property of the next Result.</typeparam>
        /// <param name="aThisResult">This Result.</param>
        /// <param name="aNextResult">Next Result to be bound after this Result.</param>
        /// <returns>Asynchronous Task that returns a Result.</returns>
        public static async Task<IHttpResult<T2>> Bind<T1, T2>(this Task<IHttpResult<T1>> aThisResult, Func<T1, Task<IHttpResult<T2>>> aNextResult)
        {
            var lThisResult = await aThisResult;
            return lThisResult.IsSuccess
                ? await aNextResult(lThisResult.Value)
                : Result.Result.Failure<T2>(lThisResult.ErrorList, lThisResult.StatusCode);

        }

        /// <summary>
        /// Binds a second Result after this one (concats railways and the respective not happy paths).
        /// </summary>
        /// <typeparam name="T1">Type of the Value property of this Result.</typeparam>
        /// <typeparam name="T2">Type of the Value property of the next Result.</typeparam>
        /// <param name="aThisResult">This Result.</param>
        /// <param name="aNextResult">Next Result to be bound after this Result.</param>
        /// <returns>Asynchronous Task that returns a Result.</returns>
        public static async Task<IHttpResult<T2>> Bind<T1, T2>(this IHttpResult<T1> aThisResult, Func<T1, Task<IHttpResult<T2>>> aNextResult)
        {
            return aThisResult.IsSuccess
                ? await aNextResult(aThisResult.Value)
                : Result.Result.Failure<T2>(aThisResult.ErrorList, aThisResult.StatusCode);

        }

        /// <summary>
        /// Returns a Task that will execute the given dead end function(as action) over this Result and then returns this same Result(continues into this same railway)).
        /// </summary>
        /// <typeparam name="T">Type of the Value property of this Result.</typeparam>
        /// <param name="aThisResult">This Result.</param>
        /// <param name="aDeadEndAction">Action to perform after the Result is calculated sucessfully.</param>
        /// <returns>Asynchronous Task that returns a Result.</returns>
        public static async Task<IHttpResult<T>> Tap<T>(this Task<IHttpResult<T>> aThisResult, Action<T> aDeadEndAction)
        {
            var lThisResult = await aThisResult;
            if (lThisResult.IsSuccess)
                aDeadEndAction(lThisResult.Value);

            return lThisResult;

        }

        /// <summary>
        /// Returns a Task that will execute the given dead end function asynchronously over this Result and then returns this same Result(continues into this same railway)).
        /// </summary>
        /// <typeparam name="T">Type of the Value property of this Result.</typeparam>
        /// <param name="aThisResult">This Result.</param>
        /// <param name="aDeadEndAsyncFunc">Action to perform after the Result is calculated sucessfully.</param>
        /// <returns>Asynchronous Task that returns a Result.</returns>
        public static async Task<IHttpResult<T>> Tap<T>(this Task<IHttpResult<T>> aThisResult, Func<T, Task> aDeadEndAsyncFunc)
        {
            var lThisResult = await aThisResult;
            if (lThisResult.IsSuccess)
                await aDeadEndAsyncFunc(lThisResult.Value);

            return lThisResult;

        }

        /// <summary>
        /// Adds a "middleware" rail that verifies that this result satisfies a given condition, if true continues otherwise switches to the failure railway with the specified HttpError.
        /// </summary>
        /// <typeparam name="T">Type of the Value property of this Result.</typeparam>
        /// <param name="aThisResult">This Result.</param>
        /// <param name="aVerifyFunction">Function that will verify if this result will continue in the happy path or not.</param>
        /// <param name="aHttpError">Error that will be sate in case the verification fails.</param>
        /// <returns>Asynchronous Task that returns a Result.</returns>
        public static async Task<IHttpResult<T>> Verify<T>(this Task<IHttpResult<T>> aThisResult, Func<T, bool> aVerifyFunction, IHttpError aHttpError)
        {
            var lThisResult = await aThisResult;
            return lThisResult.IsSuccess && !aVerifyFunction(lThisResult.Value)
                ? Result.Result.Failure<T>(aHttpError)
                : lThisResult;
        }

        /// <summary>
        /// Returns a Task that maps the Result returned from this Task to the given Result type from the given Map function(Replaces the continuation of the happy path of this railway by the given map function in case it is sucessful).
        /// </summary>
        /// <typeparam name="T1">Source Type of this Result.</typeparam>
        /// <typeparam name="T2">Target Type to map this Result.</typeparam>
        /// <param name="aThisResult">This Result.</param>
        /// <param name="aMapSuccessFunction">The mapping function to map from this Result Type to the desired Result Type.</param>
        /// <returns>Asynchronous Task that returns a Result.</returns>
        public static async Task<IHttpResult<T2>> Map<T1, T2>(this Task<IHttpResult<T1>> aThisResult, Func<T1, T2> aMapSuccessFunction)
        {
            var lThisResult = await aThisResult;
            return lThisResult.IsSuccess
                ? Result.Result.Success(aMapSuccessFunction(lThisResult.Value), lThisResult.StatusCode)
                : Result.Result.Failure<T2>(lThisResult.ErrorList, lThisResult.StatusCode);

        }

        /// <summary>
        /// Maps the Sucessful railway path continuation.
        /// </summary>
        /// <typeparam name="T1"></typeparam>
        /// <typeparam name="T2"></typeparam>
        /// <param name="aThisResult"></param>
        /// <param name="aMapSuccessFunction">Function that maps the path continuation fo the Sucess railway.</param>
        /// <returns>Next <see cref="IHttpResult{T}"/>.</returns>
        public static async Task<IHttpResult<T2>> Map<T1, T2>(this Task<IHttpResult<T1>> aThisResult, Func<T1, Task<T2>> aMapSuccessFunction)
        {
            var lThisResult = await aThisResult;
            return lThisResult.IsSuccess
                ? Result.Result.Success(await aMapSuccessFunction(lThisResult.Value), lThisResult.StatusCode)
                : Result.Result.Failure<T2>(lThisResult.ErrorList, lThisResult.StatusCode);

        }

        /// <summary>
        /// Maps the Sucessful railway path and the Failure railway path continuation.
        /// </summary>
        /// <typeparam name="T1"></typeparam>
        /// <typeparam name="T2"></typeparam>
        /// <param name="aThisResult"></param>
        /// <param name="aMapSuccessFunction">Function that maps the path continuation fo the Sucess railway.</param>
        /// <param name="aMapFailureFunction">Function that maps the path continuation fo the Failure railway.</param>
        /// <returns>Next <see cref="IHttpResult{T}"/>.</returns>
        public static async Task<IHttpResult<T2>> DoubleMap<T1, T2>(this Task<IHttpResult<T1>> aThisResult, Func<T1, Task<T2>> aMapSuccessFunction, Func<T1, Task<T2>> aMapFailureFunction)
        {
            var lThisResult = await aThisResult;
            if (lThisResult.IsSuccess)
                return Result.Result.Success(await aMapSuccessFunction(lThisResult.Value), lThisResult.StatusCode);

            await aMapFailureFunction(lThisResult.Value);
            return Result.Result.Failure<T2>(lThisResult.ErrorList, lThisResult.StatusCode);

        }

        /// <summary>
        /// Maps the Sucessful railway path and the Failure railway path continuation.
        /// </summary>
        /// <typeparam name="T1"></typeparam>
        /// <typeparam name="T2"></typeparam>
        /// <param name="aThisResult"></param>
        /// <param name="aMapSuccessFunction">Function that maps the path continuation fo the Sucess railway.</param>
        /// <param name="aMapFailureFunction">Function that maps the path continuation fo the Failure railway.</param>
        /// <returns>Next <see cref="IHttpResult{T}"/>.</returns>
        public static async Task<IHttpResult<T2>> DoubleMap<T1, T2>(this Task<IHttpResult<T1>> aThisResult, Func<T1, Task<T2>> aMapSuccessFunction, Func<T1, T2> aMapFailureFunction)
        {
            var lThisResult = await aThisResult;
            if (lThisResult.IsSuccess)
                return Result.Result.Success(await aMapSuccessFunction(lThisResult.Value), lThisResult.StatusCode);

            aMapFailureFunction(lThisResult.Value);
            return Result.Result.Failure<T2>(lThisResult.ErrorList, lThisResult.StatusCode);

        }

    }
}
