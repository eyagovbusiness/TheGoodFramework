using System.Collections.Immutable;
using System.Net;
using TGF.Common.ROP.Errors;
using TGF.Common.ROP.Result;

namespace TGF.Common.ROP.HttpResult
{
    /// <summary>
    /// Internal class that represents the unit of information while returning the result of operations under an HTTP context in Reailway Oriented Programming.
    /// </summary>
    /// <typeparam name="T">Type of the result Value.</typeparam>
    internal readonly struct HttpResult<T> : IHttpResult<T>
    {
        private Result<T> Result { get; }
        public HttpStatusCode StatusCode { get; }

        public T Value => Result.Value;

        public bool IsSuccess => Result.IsSuccess;

        public ImmutableArray<IError> ErrorList => Result.ErrorList;

        public HttpResult(T aValue, HttpStatusCode aHttpStatusCode)
        {
            StatusCode = aHttpStatusCode;
            Result = new Result<T>(aValue);
        }
        public HttpResult(ImmutableArray<IError> aErrorList, HttpStatusCode aHttpStatusCode)
        {
            StatusCode = aHttpStatusCode;
            Result = new Result<T>(aErrorList);
        }

        public override string ToString()
        {
            return $"HttpResult code = {StatusCode}, " + (IsSuccess ? $"Success Result: Value = {Value}" : $"Failure Result: Errors = {string.Join(", ", ErrorList)}");
        }

    }
}
