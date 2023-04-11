using Newtonsoft.Json;
using System.Net;
using System.Text.Json.Serialization;
using TGF.Common.ROP.Result;

namespace TGF.Common.ROP.HttpResult
{
    /// <summary>
    /// Public interface for any <see cref="HttpResult{T}"/>.
    /// </summary>
    [JsonObject]
    public interface IHttpResult<T> : IResult<T>
    {
        [JsonPropertyName("StatusCode")]
        HttpStatusCode StatusCode { get; }
    }
}
