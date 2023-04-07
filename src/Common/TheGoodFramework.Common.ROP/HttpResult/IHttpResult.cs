using System.Net;
using TGF.Common.ROP.Result;

namespace TGF.Common.ROP.HttpResult
{
    /// <summary>
    /// Public interface for any <see cref="HttpResult{T}"/>.
    /// </summary>
    public interface IHttpResult<T> : IResult<T>
    {
        HttpStatusCode StatusCode { get; }
    }
}
