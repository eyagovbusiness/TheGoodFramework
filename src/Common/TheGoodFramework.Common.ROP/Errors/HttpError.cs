using System.Net;

namespace TGF.Common.ROP.Errors
{
    /// <summary>
    /// Public interface of any HttpError
    /// </summary>
    public interface IHttpError
    {
        IError Error { get; }
        HttpStatusCode StatusCode { get; }
    }

    /// <summary>
    /// Struct of an HttpError.
    /// </summary>
    public readonly struct HttpError : IHttpError
    {
        public IError Error { get; }
        public HttpStatusCode StatusCode { get; }
        public HttpError(IError aError, HttpStatusCode aStatusCode)
        {
            Error = aError;
            StatusCode = aStatusCode;
        }
    }
}
