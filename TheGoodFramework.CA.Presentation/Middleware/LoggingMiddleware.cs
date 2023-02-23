using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging;

namespace TheGoodFramework.CA.Presentation.Middleware
{
    public class LoggingMiddleware
    {
        private readonly RequestDelegate _Next;
        private readonly ILogger _Logger;   
        public LoggingMiddleware(RequestDelegate aNext, ILogger aLogger) 
        { 
            _Next = aNext;
            _Logger = aLogger;
        }

        public async Task Invoke(HttpContext aHttpContext)
        {
            try
            {
                await _Next(aHttpContext);
            }
            catch(Exception lException)
            {
                _Logger.LogError(lException.ToString());
            }
        }
    }
}