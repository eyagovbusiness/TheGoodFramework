using System.Collections.Immutable;

namespace TGF.Common.ROP
{

    /// <summary>
    /// Class that represents the unit of information while returning the result of operations in Reailway Oriented Programming.
    /// </summary>
    /// <typeparam name="T">Type of the result Value.</typeparam>
    public class Result<T> 
    {
        public T Value { get;}
        public bool IsSuccess => ErrorList.Length == 0;
        public ImmutableArray<Error> ErrorList { get; }

        public Result(T aValue) : base()
        {
            Value = aValue;
            ErrorList = ImmutableArray<Error>.Empty;
        }
        public Result(ImmutableArray<Error> aErrorList) 
        {
            Value = default(T);
            if (aErrorList.Length == 0)
                throw new InvalidOperationException("Can't create a failure Result without errors.");

            ErrorList = aErrorList;
        }

    }

    /// <summary>
    /// Static class to create new instances of Results with success or failure(s).
    /// </summary>
    public static class Result
    {
        public static Result<T> Success<T>(this T aValue) => new Result<T>(aValue);
        public static Result<T> Failure<T>(ImmutableArray<Error> aErrorList) => new Result<T>(aErrorList);
        public static Result<T> Failure<T>(Error aError) => new Result<T>(ImmutableArray.Create(aError));
    }

}