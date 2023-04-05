using System.Collections.Immutable;
using System.Runtime.ExceptionServices;

namespace TGF.Common.ROP
{
    /// <summary>
    /// Static class to register operations between Results like binding Results, mapping different Results or executing certain actions after the Result is generated.
    /// </summary>
    public static class ResultOperations
    {

        /// <summary>
        /// Bunds a second Result after this one.
        /// </summary>
        /// <typeparam name="T1">Type of the Value property of this Result.</typeparam>
        /// <typeparam name="T2">Type of the Value property of the next Result.</typeparam>
        /// <param name="aThisResult">This Result.</param>
        /// <param name="aNextResult">Next Result to be bound after this Result.</param>
        /// <returns>Asynchronous Task that returns a Result.</returns>
        public static async Task<Result<T2>> Bind<T1, T2>(this Task<Result<T1>> aThisResult, Func<T1, Task<Result<T2>>> aNextResult)
        {
            var r = await aThisResult;
            return r.IsSuccess
                ? await aNextResult(r.Value)
                : Result.Failure<T2>(r.ErrorList);

        }

        /// <summary>
        /// Returns a Task that will execute the given Action after computing the Task that will return this Result.
        /// </summary>
        /// <typeparam name="T">Type of the Value property of this Result.</typeparam>
        /// <param name="aThisResult">This Result.</param>
        /// <param name="aAction">Action to perform after the Result is calculated sucessfully.</param>
        /// <returns>Asynchronous Task that returns a Result.</returns>
        public static async Task<Result<T>> Then<T>(this Task<Result<T>> aThisResult, Action<T> aAction)
        {
            var lThisResult = await aThisResult;
            if (lThisResult.IsSuccess)
                aAction(lThisResult.Value);

            return lThisResult;

        }

        /// <summary>
        /// Returns a Task that maps the Result returned from this Task to the given Result type from the given Map function.
        /// </summary>
        /// <typeparam name="T1">Source Type of this Result.</typeparam>
        /// <typeparam name="T2">Target Type to map this Result.</typeparam>
        /// <param name="aThisResult">This Result.</param>
        /// <param name="aMapFunction">The mapping function to map from this Result Type to the desired Result Type.</param>
        /// <returns>Asynchronous Task that returns a Result.</returns>
        public static async Task<Result<T2>> Map<T1, T2>(this Task<Result<T1>> aThisResult, Func<T1, T2> aMapFunction)
        {
            var lThisResult = await aThisResult;
            return lThisResult.IsSuccess
                ? Result.Success(aMapFunction(lThisResult.Value))
                : Result.Failure<T2>(lThisResult.ErrorList);

        }

    }
}
