namespace TGF.Common.ROP.Result
{
    /// <summary>
    /// Static class to register operations between Results like binding Results, mapping different Results or executing certain actions after the Result is generated.
    /// </summary>
    public static class RailwaySwitchExtensions
    {

        /// <summary>
        /// Bunds a second Result after this one (concats railways and the respective not happy paths).
        /// </summary>
        /// <typeparam name="T1">Type of the Value property of this Result.</typeparam>
        /// <typeparam name="T2">Type of the Value property of the next Result.</typeparam>
        /// <param name="aThisResult">This Result.</param>
        /// <param name="aNextResult">Next Result to be bound after this Result.</param>
        /// <returns>Asynchronous Task that returns a Result.</returns>
        public static async Task<IResult<T2>> Bind<T1, T2>(this Task<IResult<T1>> aThisResult, Func<T1, Task<IResult<T2>>> aNextResult)
        {
            var lThisResult = await aThisResult;
            return lThisResult.IsSuccess
                ? await aNextResult(lThisResult.Value)
                : Result.Failure<T2>(lThisResult.ErrorList);

        }

        /// <summary>
        /// Returns a Task that will execute the given dead end function(as action) over this Result and then returns this same Result(continues into this same railway)).
        /// </summary>
        /// <typeparam name="T">Type of the Value property of this Result.</typeparam>
        /// <param name="aThisResult">This Result.</param>
        /// <param name="aAction">Action to perform after the Result is calculated sucessfully.</param>
        /// <returns>Asynchronous Task that returns a Result.</returns>
        public static async Task<IResult<T>> Tap<T>(this Task<IResult<T>> aThisResult, Action<T> aDeadEndAction)
        {
            var lThisResult = await aThisResult;
            if (lThisResult.IsSuccess)
                aDeadEndAction(lThisResult.Value);

            return lThisResult;

        }

        /// <summary>
        /// Returns a Task that maps the Result returned from this Task to the given Result type from the given Map function(Replaces the continuation of the happy path of this railway by the given map function in case it is sucessful).
        /// </summary>
        /// <typeparam name="T1">Source Type of this Result.</typeparam>
        /// <typeparam name="T2">Target Type to map this Result.</typeparam>
        /// <param name="aThisResult">This Result.</param>
        /// <param name="aMapFunction">The mapping function to map from this Result Type to the desired Result Type.</param>
        /// <returns>Asynchronous Task that returns a Result.</returns>
        public static async Task<IResult<T2>> Map<T1, T2>(this Task<IResult<T1>> aThisResult, Func<T1, T2> aMapFunction)
        {
            var lThisResult = await aThisResult;
            return lThisResult.IsSuccess
                ? Result.Success(aMapFunction(lThisResult.Value))
                : Result.Failure<T2>(lThisResult.ErrorList);

        }

    }
}
