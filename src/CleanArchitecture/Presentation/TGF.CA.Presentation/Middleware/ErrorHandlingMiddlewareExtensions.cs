using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.Features;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Primitives;
using System.Diagnostics;
using TGF.CA.Application.Contracts.Routing;

namespace TGF.CA.Presentation.Middleware
{
    public static class ErrorHandlingMiddlewareExtensions
    {
        /// <summary>Fixed detail returned for every unexpected failure: exception text can carry secrets, connection strings or provider values.</summary>
        public const string UnexpectedErrorDetail = "An unexpected error occurred while processing the request.";

        /// <summary>
        /// Intercepts unexpected exceptions at this pipeline position before the framework's exception handler/server can log the raw exception,
        /// and answers with a fixed 500 Problem response when (and only when) the response is provably clean; otherwise the connection is aborted.
        /// Status pages and the safe <see cref="TGFEndpointRoutes.error"/> route are retained.
        /// </summary>
        /// <remarks>
        /// This is the shared HTTP failure-privacy boundary, not a liveness or licensing check. It prevents unexpected
        /// exception details from reaching clients or upstream exception logging. Response lifecycle guards also prevent
        /// pending output or deferred callbacks from contaminating a replacement error response. It does not buffer normal responses.
        /// </remarks>
        public static WebApplication UseCustomErrorHandlingMiddleware(this WebApplication aWebApplication)
        {
            aWebApplication.UseMiddleware<SafeExceptionMiddleware>();
            aWebApplication.UseStatusCodePages();
            aWebApplication.Map(TGFEndpointRoutes.error, (HttpContext aHttpContext) =>
                Results.Problem(detail: UnexpectedErrorDetail, extensions: CreateExtensions(aHttpContext)));
            return aWebApplication;
        }

        internal static Dictionary<string, object?> CreateExtensions(HttpContext aHttpContext)
            => new() { { "traceId", Activity.Current?.Id ?? aHttpContext.TraceIdentifier } };
    }

    /// <summary>
    /// Contains unexpected downstream HTTP failures without exposing raw exception details in responses or server logs.
    /// Returns a fixed 500 Problem response only when the original response can be safely replaced; otherwise aborts the connection.
    /// </summary>
    /// <remarks>
    /// A false <see cref="HttpResponse.HasStarted"/> alone does not prove that the body writer has no pending bytes.
    /// Clearing headers also does not remove deferred OnStarting callbacks. The request-local body and callback guards
    /// supply the additional evidence needed to avoid flushing failed-response data or recreating its headers during error handling.
    /// Only captured, allowlisted security headers survive a clean reset. Normal responses continue through the original features;
    /// no response buffering, licensing evaluation or health-check execution is introduced here.
    /// </remarks>
    internal sealed class SafeExceptionMiddleware(RequestDelegate aNext, ILogger<SafeExceptionMiddleware> aLogger)
    {
        /// <summary>
        /// Locally generated security headers (the Manager sets them immediately in its own security middleware). Only these are restored on a clean error;
        /// nothing else present at entry (cookies, content metadata, Location, request or provider values) is trusted.
        /// </summary>
        internal static readonly string[] SecurityHeaderAllowlist =
        [
            "X-Frame-Options",
            "X-Content-Type-Options",
            "X-XSS-Protection",
            "Referrer-Policy",
            "Permissions-Policy",
            "Content-Security-Policy",
            "Strict-Transport-Security",
        ];

        public async Task Invoke(HttpContext aHttpContext)
        {
            var lScope = LifecycleScope.Begin(aHttpContext);
            try
            {
                await aNext(aHttpContext);
            }
            catch (OperationCanceledException) when (aHttpContext.RequestAborted.IsCancellationRequested)
            {
                // The client went away: not a business failure. Pending or uncertain output is never flushed on the way out; the connection is terminated.
                aLogger.LogInformation("Request was aborted by the client. TraceId: {TraceId}", aHttpContext.TraceIdentifier);
                if (lScope.State.PendingBytes > 0 || lScope.State.UncertainOutput)
                {
                    lScope.Abort(aHttpContext);
                }
            }
            catch (Exception lException)
            {
                await HandleAsync(aHttpContext, lScope, lException.GetType().Name);
            }
            finally
            {
                lScope.Restore(aHttpContext);
            }
        }

        private async Task HandleAsync(HttpContext aHttpContext, LifecycleScope aScope, string aExceptionType)
        {
            // Only the exception type and trace id are logged; the exception object, its message, inner chain and Data are dropped here.
            aLogger.LogError("Unhandled request failure. TraceId: {TraceId}. Exception type: {ExceptionType}", aHttpContext.TraceIdentifier, aExceptionType);

            // Entered BEFORE any reset or write so deferred downstream callbacks cannot recreate values of the failed response.
            aScope.State.EnterSafeErrorPreparation();

            var lUnsafeReason = aScope.FindUnsafeReason(aHttpContext);
            if (lUnsafeReason is not null)
            {
                // Fixed events only: never callback state, headers, paths, queries or bodies.
                aLogger.LogError("{Reason} The connection was aborted. TraceId: {TraceId}", lUnsafeReason, aHttpContext.TraceIdentifier);
                aScope.Abort(aHttpContext);
                return;
            }

            try
            {
                aScope.PrepareCleanResponse(aHttpContext);
                await Results.Problem(detail: ErrorHandlingMiddlewareExtensions.UnexpectedErrorDetail, extensions: ErrorHandlingMiddlewareExtensions.CreateExtensions(aHttpContext)).ExecuteAsync(aHttpContext);
            }
            catch (Exception)
            {
                // Failing to write the safe response must not route the original (or this) exception into server logs.
                aLogger.LogError("The fixed error response could not be written; the connection was aborted. TraceId: {TraceId}", aHttpContext.TraceIdentifier);
                aScope.Abort(aHttpContext);
            }
        }

        /// <summary>Request-local ownership of the response/body feature guards, the callback state and the trusted security header snapshot.</summary>
        private sealed class LifecycleScope
        {
            private readonly IHttpResponseFeature? _originalResponse;
            private readonly IHttpResponseBodyFeature? _originalBody;
            private readonly GuardedResponseFeature? _ownedResponse;
            private readonly GuardedResponseBodyFeature? _ownedBody;
            private readonly KeyValuePair<string, string[]>[] _securityHeaders;

            private LifecycleScope(ResponseLifecycleState aState, IHttpResponseFeature? aOriginalResponse, IHttpResponseBodyFeature? aOriginalBody, KeyValuePair<string, string[]>[] aSecurityHeaders)
            {
                State = aState;
                _originalResponse = aOriginalResponse;
                _originalBody = aOriginalBody;
                _securityHeaders = aSecurityHeaders;
                _ownedResponse = aOriginalResponse is null ? null : new GuardedResponseFeature(aOriginalResponse, aState);
                _ownedBody = aOriginalBody is null ? null : new GuardedResponseBodyFeature(aOriginalBody, aState);
            }

            public ResponseLifecycleState State { get; }

            public static LifecycleScope Begin(HttpContext aHttpContext)
            {
                // Values are cloned: later mutation of the live header storage must not alter the trusted snapshot.
                var lSecurityHeaders = new List<KeyValuePair<string, string[]>>();
                foreach (var lName in SecurityHeaderAllowlist)
                {
                    if (aHttpContext.Response.Headers.TryGetValue(lName, out var lValues) && lValues.Count > 0)
                    {
                        lSecurityHeaders.Add(new KeyValuePair<string, string[]>(lName, lValues.ToArray().Select(value => value ?? string.Empty).ToArray()));
                    }
                }

                var lScope = new LifecycleScope(
                    new ResponseLifecycleState(),
                    aHttpContext.Features.Get<IHttpResponseFeature>(),
                    aHttpContext.Features.Get<IHttpResponseBodyFeature>(),
                    [.. lSecurityHeaders]);

                if (lScope._ownedResponse is not null)
                {
                    aHttpContext.Features.Set<IHttpResponseFeature>(lScope._ownedResponse);
                }

                if (lScope._ownedBody is not null)
                {
                    aHttpContext.Features.Set<IHttpResponseBodyFeature>(lScope._ownedBody);
                }

                return lScope;
            }

            /// <summary>Returns a fixed description when a clean reset cannot be proven; null when a clean 500 is safe.</summary>
            public string? FindUnsafeReason(HttpContext aHttpContext)
            {
                if (_ownedResponse is null || _ownedBody is null || _originalBody is null)
                {
                    return "Response feature ownership is unknown.";
                }

                if (!ReferenceEquals(aHttpContext.Features.Get<IHttpResponseFeature>(), _ownedResponse)
                    || !ReferenceEquals(aHttpContext.Features.Get<IHttpResponseBodyFeature>(), _ownedBody))
                {
                    return "Response feature ownership was lost to a replacement.";
                }

                if (aHttpContext.Response.HasStarted)
                {
                    return "Response had already started.";
                }

                if (State.CallbackFailure)
                {
                    return "A response callback failed.";
                }

                if (State.PendingBytes > 0 || State.UncertainOutput)
                {
                    return "Unflushed or uncertain response output could not be discarded.";
                }

                return null;
            }

            /// <summary>Clears pending status/headers/content metadata, restores only the trusted allowlisted values (once) and switches to the proven-clean original body.</summary>
            public void PrepareCleanResponse(HttpContext aHttpContext)
            {
                // Zero pending/uncertain output was proven in FindUnsafeReason, so the known original body feature may be used directly.
                aHttpContext.Features.Set<IHttpResponseBodyFeature>(_originalBody);

                var lResponse = aHttpContext.Response;
                lResponse.Headers.Clear();
                lResponse.ContentLength = null;
                lResponse.ContentType = null;
                lResponse.StatusCode = StatusCodes.Status500InternalServerError;
                _ownedResponse!.ReasonPhrase = null;
                foreach (var lHeader in _securityHeaders)
                {
                    lResponse.Headers[lHeader.Key] = new StringValues([.. lHeader.Value]);
                }
            }

            public void Abort(HttpContext aHttpContext)
            {
                State.EnterAborted();
                aHttpContext.Abort();
            }

            /// <summary>Restores an original feature only when the current reference is still this guard; a downstream replacement is never overwritten.</summary>
            public void Restore(HttpContext aHttpContext)
            {
                if (_ownedResponse is not null && ReferenceEquals(aHttpContext.Features.Get<IHttpResponseFeature>(), _ownedResponse))
                {
                    aHttpContext.Features.Set<IHttpResponseFeature>(_originalResponse);
                }

                if (_ownedBody is not null && ReferenceEquals(aHttpContext.Features.Get<IHttpResponseBodyFeature>(), _ownedBody))
                {
                    aHttpContext.Features.Set<IHttpResponseBodyFeature>(_originalBody);
                }
            }
        }
    }
}
