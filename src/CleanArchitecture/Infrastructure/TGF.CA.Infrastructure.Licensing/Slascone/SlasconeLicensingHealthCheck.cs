using Microsoft.Extensions.Diagnostics.HealthChecks;
using TGF.CA.Infrastructure.Licensing.Slascone.Contracts;
using TGF.CA.Infrastructure.Licensing.Slascone.Services;

namespace TGF.CA.Infrastructure.Licensing.Slascone;

/// <summary>
/// Health check that verifies if a floating license session is currently open.
/// </summary>
internal sealed class SlasconeLicensingHealthCheck(ILicensingService licensingService, SlasconeLicensingHealthCheckCacheService healthCheckCache) : IHealthCheck {
    public Task<HealthCheckResult> CheckHealthAsync(HealthCheckContext _, CancellationToken __) {
        var newCheckResult = licensingService.ComplianceStatus switch {
            LicenseComplianceStatus.Compliant => HealthCheckResult.Healthy("License key is valid, license is active and a floating seat was assigned to this deployment, session established."),
            LicenseComplianceStatus.NonCompliant => HealthCheckResult.Unhealthy("License key is invalid OR license is innactive OR no floating seat was assigned, session not established."),

            //TODO: Provide more detailed health check results
            //LicensingStatus.SessionHeartbeatSuccess => HealthCheckResult.Healthy("Floating session is open and the last heartbeat was successful"),
            //LicensingStatus.SessionHeartbeatFailed => HealthCheckResult.Unhealthy("Floating session is open but the last heartbeat failed"),
            //LicensingStatus.ActivationFailed => HealthCheckResult.Unhealthy("License key activation failed"),
            //LicensingStatus.SessionOpenFailed => HealthCheckResult.Unhealthy("Open floating session failed"),
            //LicensingStatus.SessionClosed => HealthCheckResult.Unhealthy("Floating session closed"),
            //LicensingStatus.SessionCloseFailed => HealthCheckResult.Degraded("Closing floating session failed"),

            _ => HealthCheckResult.Unhealthy("No license compliance evaluation executed yet. This software will only run under license compliant systems.")
        };
        healthCheckCache.SetLastResult(newCheckResult);
        return Task.FromResult(newCheckResult);
    }

}
