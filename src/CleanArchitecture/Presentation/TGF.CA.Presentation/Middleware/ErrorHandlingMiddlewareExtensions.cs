using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Diagnostics;
using Microsoft.AspNetCore.Http;
using System.Diagnostics;
using TGF.CA.Application.Contracts.Routing;

namespace TGF.CA.Presentation.Middleware
{
    public static class ErrorHandlingMiddlewareExtensions
    {
        public static WebApplication UseCustomErrorHandlingMiddleware(this WebApplication aWebApplication)
        {
            aWebApplication.UseExceptionHandler(TGFEndpointRoutes.error);
            aWebApplication.UseStatusCodePages();
            aWebApplication.Map(TGFEndpointRoutes.error, (HttpContext aHttpContext) =>
            {
                Exception? lException = aHttpContext.Features.Get<IExceptionHandlerFeature?>()?.Error;
                var lExtensions = new Dictionary<string, object?>
                {
                    { "traceId",  Activity.Current?.Id ?? aHttpContext?.TraceIdentifier }
                };
                return lException != null
                    ? Results.Problem(detail: lException.Message, extensions: lExtensions)
                    : Results.Problem(extensions: lExtensions);
            });
            return aWebApplication;
        }
    }
}