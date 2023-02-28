using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging;

namespace TGF.CA.Presentation.Middleware
{
    public class LoggingMiddleware
    {
        private readonly RequestDelegate _Next;
        private readonly ILogger _Logger;
        public LoggingMiddleware(RequestDelegate aNext, ILoggerFactory aLoggerFactory)
        {
            _Next = aNext;
            _Logger = aLoggerFactory.CreateLogger(typeof(LoggingMiddleware));
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
            }
        }
    }
}