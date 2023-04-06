using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Text;
using System.Threading.Tasks;

namespace TGF.Common.ROP.Errors
{
    public interface IHttpError
    {
        IError Error { get; }
        HttpStatusCode StatusCode { get; }
    }

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
