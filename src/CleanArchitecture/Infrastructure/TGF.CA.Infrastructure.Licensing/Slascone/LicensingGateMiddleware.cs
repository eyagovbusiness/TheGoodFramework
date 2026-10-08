using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;
using System.Net;
using TGF.CA.Application.Contracts.Routing;
using TGF.CA.Infrastructure.Licensing.Slascone.Contracts;

namespace TGF.CA.Infrastructure.Licensing.Slascone;

/// <summary>
/// Middleware that blocks all requests unless a valid license session is open.
/// </summary>
internal sealed class LicensingGateMiddleware {
    private const string UnavailableDetail = "The service is unavailable because license compliance requirements are not met.";

    private readonly RequestDelegate _next;
    private readonly HashSet<PathString> _allow;

    public LicensingGateMiddleware(RequestDelegate next, IEnumerable<string> allowedPaths) {
        _next = next;
        _allow = [TGFEndpointRoutes.health, TGFEndpointRoutes.healthUi];
        foreach (var p in allowedPaths)
            _allow.Add(new PathString(p));
    }

    // Dependencies are deliberately not Invoke parameters: the middleware pipeline would resolve them before the exemption check below.
    public async Task Invoke(HttpContext httpContext) {
        // Allow some infrastructure endpoints to pass (readiness/liveness/preStop, metrics, ...)
        if (_allow.Contains(httpContext.Request.Path)) {
            await _next(httpContext);
            return;
        }

        LicenseComplianceStatus complianceStatus;
        try {
            complianceStatus = httpContext.RequestServices.GetRequiredService<ILicensingService>().ComplianceStatus;
        }
        catch (Exception) {
            // Fail closed: the request is never admitted. The original exception (which may carry secrets or provider text) is replaced
            // by a fixed one before it can reach upstream handlers or server logs.
            throw new InvalidOperationException("License compliance could not be evaluated.");
        }

        if (complianceStatus is not LicenseComplianceStatus.Compliant) {
            httpContext.Response.StatusCode = (int)HttpStatusCode.ServiceUnavailable;
            httpContext.Response.ContentType = "application/problem+json";

            // Fixed text: cached health descriptions or provider details must never reach unauthenticated callers.
            var response = new {
                title = "Service unavailable",
                detail = UnavailableDetail,
                status = 503,
            };

            await httpContext.Response.WriteAsync(System.Text.Json.JsonSerializer.Serialize(response));
            return;
        }
        await _next(httpContext);
    }

}

