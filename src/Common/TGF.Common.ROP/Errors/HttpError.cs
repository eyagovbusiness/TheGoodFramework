using Newtonsoft.Json;
using System.Net;
using System.Text.Json.Serialization;

namespace TGF.Common.ROP.Errors {
    /// <summary>
    /// Public interface of any HttpError
    /// </summary>
    [JsonObject]
    public interface IHttpError {
        [JsonPropertyName("Error")]
        IError Error { get; }
        [JsonPropertyName("StatusCode")]
        HttpStatusCode StatusCode { get; }
    }

    /// <summary>
    /// Struct of an HttpError.
    /// </summary>
    public readonly struct HttpError(IError aError, HttpStatusCode aStatusCode) : IHttpError {
        public IError Error { get; } = aError;
        public HttpStatusCode StatusCode { get; } = aStatusCode;

        public override string ToString() => $"HttpErrorCode({StatusCode}) => {Error}";
    }
}
