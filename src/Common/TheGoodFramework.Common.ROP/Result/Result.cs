using Microsoft.AspNetCore.Http;
using System.Collections.Immutable;
using System.Net;
using TGF.Common.ROP.Errors;
using TGF.Common.ROP.HttpResult;

namespace TGF.Common.ROP.Result
{

    /// <summary>
    /// Class that represents the unit of information while returning the result of operations in Reailway Oriented Programming.
    /// </summary>
    /// <typeparam name="T">Type of the result Value.</typeparam>
    public class Result<T> : IResult<T>
    {
        public IResult<T>[] ResultCarryList { get; private set; }
        public T Value { get; }
        public bool IsSuccess => ErrorList.Length == 0;
        public ImmutableArray<IError> ErrorList { get; }

        public Result(T aValue) : base()
        {
            Value = aValue;
            ErrorList = ImmutableArray<IError>.Empty;
        }
        public Result(ImmutableArray<IError> aErrorList)
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
        #region Result
        public static IResult<T> Success<T>(T aValue) => new Result<T>(aValue);
        public static IResult<T> Failure<T>(ImmutableArray<IError> aErrorList) => new Result<T>(aErrorList);
        public static IResult<T> Failure<T>(IError aError) => new Result<T>(ImmutableArray.Create(aError));
        #endregion

        #region HttpResult
        public static IResult<T> Success<T>(T aValue, HttpStatusCode aStatusCode) => new HttpResult<T>(aValue, aStatusCode);
        public static IResult<T> SuccessHttp<T>(T aValue, HttpStatusCode aStatusCode = HttpStatusCode.OK) => new HttpResult<T>(aValue, aStatusCode);
        public static IResult<T> Failure<T>(ImmutableArray<IError> aErrorList, HttpStatusCode aStatusCode) => new HttpResult<T>(aErrorList, aStatusCode);
        public static IResult<T> Failure<T>(IHttpError aHttpError) => new HttpResult<T>(ImmutableArray.Create(aHttpError.Error), aHttpError.StatusCode);
        #endregion
    }

}