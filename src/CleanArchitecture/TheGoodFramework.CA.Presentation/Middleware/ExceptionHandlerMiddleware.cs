using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging;
using System.Net;

namespace TGF.CA.Presentation.Middleware
{
    public class ExceptionHandlerMiddleware
    {
        private readonly RequestDelegate _Next;
        private readonly ILogger _Logger;
        public ExceptionHandlerMiddleware(RequestDelegate aNext, ILoggerFactory aLoggerFactory)
        {
            _Next = aNext;
            _Logger = aLoggerFactory.CreateLogger(typeof(ExceptionHandlerMiddleware));
        }

        public async Task Invoke(HttpContext aHttpContext)
        {
            try
            {
                await _Next(aHttpContext);
            }
            catch (Exception lException)
            {
                _Logger.LogError(lException.ToString());
                aHttpContext.Response.StatusCode = (int)HttpStatusCode.InternalServerError;
                await _Next(aHttpContext);
            }
        }
    }
}