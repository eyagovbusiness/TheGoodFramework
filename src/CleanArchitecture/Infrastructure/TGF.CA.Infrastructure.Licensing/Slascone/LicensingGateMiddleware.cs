using Microsoft.AspNetCore.Http;
using System.Net;
using TGF.CA.Application.Contracts.Routing;
using TGF.CA.Infrastructure.Licensing.Slascone.Contracts;
using TGF.CA.Infrastructure.Licensing.Slascone.Services;

namespace TGF.CA.Infrastructure.Licensing.Slascone;

/// <summary>
/// Middleware that blocks all requests unless a valid license session is open.
/// </summary>
internal sealed class LicensingGateMiddleware {
    private readonly RequestDelegate _next;
    private readonly HashSet<PathString> _allow;

    public LicensingGateMiddleware(RequestDelegate next, IEnumerable<string> allowedPaths) {
        _next = next;
        _allow = [TGFEndpointRoutes.health, TGFEndpointRoutes.healthUi];
        foreach (var p in allowedPaths)
            _allow.Add(new PathString(p));
    }

    public async Task Invoke(HttpContext httpContext, ILicensingService licenseService, SlasconeLicensingHealthCheckCacheService slasconeLicensingHealthCheckCacheService) {
        // Allow some infrastructure endpoints to pass (readiness/liveness/preStop, metrics, ...)
        if (_allow.Contains(httpContext.Request.Path)) {
            await _next(httpContext);
            return;
        }

        var lastSlasconeLicensingHealthCheck = slasconeLicensingHealthCheckCacheService.GetLastResult();

        if (licenseService.ComplianceStatus is not LicenseComplianceStatus.Compliant) {
            httpContext.Response.StatusCode = (int)HttpStatusCode.ServiceUnavailable;
            httpContext.Response.ContentType = "application/problem+json";

            // Serialize health check data into JSON
            var response = new {
                title = "Service unavailable",
                detail = lastSlasconeLicensingHealthCheck?.Description,
                status = 503,
            };

            await httpContext.Response.WriteAsync(System.Text.Json.JsonSerializer.Serialize(response));
            return;
        }
        await _next(httpContext);
    }

}

