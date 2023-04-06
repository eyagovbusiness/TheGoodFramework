using System.Collections.Immutable;
using System.Net;
using TGF.Common.ROP.Errors;
using TGF.Common.ROP.Result;

namespace TGF.Common.ROP.HttpResult
{
    /// <summary>
    /// Class that represents the unit of information while returning the result of operations under an HTTP context in Reailway Oriented Programming.
    /// </summary>
    /// <typeparam name="T">Type of the result Value.</typeparam>
    public class HttpResult<T> : Result<T>, IHttpResult<T>
    {
        public HttpStatusCode StatusCode { get; }
        public HttpResult(T aValue, HttpStatusCode aHttpStatusCode) : base(aValue)
        {
            StatusCode = aHttpStatusCode;
        }
        public HttpResult(ImmutableArray<IError> aErrorList, HttpStatusCode aHttpStatusCode) : base(aErrorList)
        {
            StatusCode = aHttpStatusCode;
        }

    }
}
