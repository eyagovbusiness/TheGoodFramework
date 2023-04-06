using System.Net;
using TGF.Common.ROP.Result;

namespace TGF.Common.ROP.HttpResult
{
    public interface IHttpResult<T> : IResult<T>
    {
        HttpStatusCode StatusCode { get; }
    }
}
