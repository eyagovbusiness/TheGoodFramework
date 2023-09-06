using Microsoft.AspNetCore.Http;
using System.Net;

namespace TGF.CA.Infrastructure.Middleware.Security
{
    /// <summary>
    /// Middleware to block requests that contain "/private/" in the URL.
    /// </summary>
    public abstract class BlockPrivateProxyingMiddlewareBase
    {
        private readonly RequestDelegate _next;
        private readonly string _privatePathSegment;

        public BlockPrivateProxyingMiddlewareBase(string aPrivateSegment, RequestDelegate aNext)
        {
            _privatePathSegment = aPrivateSegment ?? throw new ArgumentNullException(nameof(aPrivateSegment));
            _next = aNext;
        }

        /// <summary>
        /// Invokes the middleware to check the request's path.
        /// If the path contains "/private/", the request is blocked and 404 NotFound is replied bringing "security through obscurity".
        /// Otherwise, the request is passed to the next middleware.
        /// </summary>
        /// <param name="context">The current HTTP context.</param>
        /// <returns>A Task representing the asynchronous operation.</returns>
        public async Task InvokeAsync(HttpContext context)
        {
            if (context.Request.Path.Value == null || context.Request.Path.Value.Contains(_privatePathSegment, StringComparison.OrdinalIgnoreCase))
            {
                context.Response.StatusCode = (int)HttpStatusCode.NotFound; 
                return;
            }

            await _next(context);
        }
    }

    // Example for derived classes:
    //public class BlockPrivateProxyingMiddleware : BlockPrivateProxyingMiddlewareBase
    //{
    //    public BlockPrivateProxyingMiddleware(RequestDelegate next)
    //        : base(PrivateEndpointPaths.Default, next)
    //    {
    //    }
    //}

}
