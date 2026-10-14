using Microsoft.Extensions.Diagnostics.HealthChecks;
using Slascone.Client;
using TGF.CA.Infrastructure.Licensing.Slascone.Contracts;
using TGF.CA.Infrastructure.Licensing.Slascone.Services;

namespace TGF.CA.Infrastructure.Licensing.Slascone;

/// <summary>
/// Health check that verifies if a floating license session is currently open.
/// </summary>
internal sealed class SlasconeLicensingHealthCheck(ILicensingService licensingService, SlasconeLicensingHealthCheckCacheService healthCheckCache) : IHealthCheck {
    public Task<HealthCheckResult> CheckHealthAsync(HealthCheckContext _, CancellationToken __) {
        var newCheckResult = GetLicensingHealthCheckResult(
            licensingService.ComplianceStatus,
            licensingService.ActivationStatus,
            licensingService.HeartbeatStatus,
            licensingService.LastOpenSessionAttemptStatus,
            licensingService.LicenseInfo,
            licensingService.SessionInfo,
            licensingService.LastError,
            licensingService.ClientId,
            DateTimeOffset.UtcNow);

        healthCheckCache.SetLastResult(newCheckResult);
        return Task.FromResult(newCheckResult);
    }

    /// <summary>
    /// Maps cached licensing state to an operator-facing health check result without performing SLASCONE calls.
    /// </summary>
    internal static HealthCheckResult GetLicensingHealthCheckResult(
        LicenseComplianceStatus complianceStatus,
        LicenseActivationStatus activationStatus,
        LicenseHeartbeatStatus heartbeatStatus,
        LicenseSessionStatus lastOpenSessionAttemptStatus,
        LicenseInfoDto? licenseInfo,
        SessionStatusDto? sessionInfo,
        LicensingOperationError? lastError,
        string clientId,
        DateTimeOffset utcNow) {
        var data = BuildHealthData(
            complianceStatus,
            activationStatus,
            heartbeatStatus,
            lastOpenSessionAttemptStatus,
            licenseInfo,
            sessionInfo,
            lastError,
            clientId);

        if (complianceStatus is LicenseComplianceStatus.Compliant) {
            var validUntil = FormatTimestamp(sessionInfo?.Session_valid_until);
            return HealthCheckResult.Healthy(
                AppendContext($"SLASCONE licensing is compliant: the license is valid, the heartbeat succeeded, and a floating session is assigned to this deployment until {validUntil}.", clientId, lastError),
                data);
        }

        if (lastError?.ErrorCode is SlasconeApiErrorCode.EXCEEDED_ALLOWED_CONNECTIONS) {
            var maxOpenSessionCount = sessionInfo is null ? string.Empty : $" (Max_open_session_count: {sessionInfo.Max_open_session_count})";
            return HealthCheckResult.Unhealthy(
                AppendContext($"SLASCONE floating license seats exhausted: the maximum number of concurrent floating sessions/seats for this license is exhausted{maxOpenSessionCount}. Release stale sessions or clients in the SLASCONE portal.", clientId, lastError),
                data: data);
        }

        if (lastError?.ErrorCode is SlasconeApiErrorCode.UNKNOWN_CLIENT) {
            return HealthCheckResult.Unhealthy(
                AppendContext("SLASCONE rejected this deployment because its client id is not activated against the license.", clientId, lastError),
                data: data);
        }

        if (lastError?.ErrorCode is SlasconeApiErrorCode.TOKEN_ALREADY_ASSINGED) {
            return HealthCheckResult.Unhealthy(
                AppendContext("SLASCONE rejected license activation because the token is already assigned to another device.", clientId, lastError),
                data: data);
        }

        if (lastError?.ErrorCode is SlasconeApiErrorCode.EXPIRED_KEY) {
            return HealthCheckResult.Unhealthy(
                AppendContext("SLASCONE rejected the license because the license key has expired.", clientId, lastError),
                data: data);
        }

        if (lastError?.ErrorCode is SlasconeApiErrorCode.INVALID_KEY) {
            return HealthCheckResult.Unhealthy(
                AppendContext("SLASCONE rejected the license because the license key is invalid.", clientId, lastError),
                data: data);
        }

        if (lastError?.ErrorCode is SlasconeApiErrorCode.NOT_ACTIVATED) {
            return HealthCheckResult.Unhealthy(
                AppendContext("SLASCONE rejected the license because it has not been activated for this deployment.", clientId, lastError),
                data: data);
        }

        if (lastError?.ErrorCode is SlasconeApiErrorCode.NON_COMPLIANT_VERSION) {
            return HealthCheckResult.Unhealthy(
                AppendContext("SLASCONE rejected the license because this software version is not compliant with the license.", clientId, lastError),
                data: data);
        }

        if (lastOpenSessionAttemptStatus is LicenseSessionStatus.CloseFailed) {
            return HealthCheckResult.Degraded(
                AppendContext("SLASCONE floating session close failed; the seat may not be freed until server-side session expiry.", clientId, lastError),
                data: data);
        }

        if (heartbeatStatus is LicenseHeartbeatStatus.Failed && IsSessionStillValid(sessionInfo, utcNow)) {
            return HealthCheckResult.Degraded(
                AppendContext($"SLASCONE heartbeat failed, but the existing floating session is still valid until {FormatTimestamp(sessionInfo?.Session_valid_until)}; this is likely transient and the seat is still held.", clientId, lastError),
                data: data);
        }

        if (lastError?.ErrorCode is SlasconeApiErrorCode.API_CRASH) {
            return HealthCheckResult.Unhealthy(
                AppendContext("SLASCONE API is unreachable or the local licensing dependency failed; this is distinct from a licensing violation.", clientId, lastError),
                data: data);
        }

        if (lastOpenSessionAttemptStatus is LicenseSessionStatus.OpenFailed && heartbeatStatus is LicenseHeartbeatStatus.Success) {
            return HealthCheckResult.Unhealthy(
                AppendContext("SLASCONE heartbeat succeeded, but opening or renewing the floating session failed; the license may be valid while no floating seat is assigned to this deployment.", clientId, lastError),
                data: data);
        }

        if (lastOpenSessionAttemptStatus is LicenseSessionStatus.Opened && IsSessionExpired(sessionInfo, utcNow)) {
            var sessionValidUntil = sessionInfo!.Session_valid_until!.Value;
            var overdue = utcNow - sessionValidUntil;
            return HealthCheckResult.Unhealthy(
                AppendContext($"SLASCONE floating session was previously opened but expired at {FormatTimestamp(sessionValidUntil)}; renewal failed and is overdue by {FormatDuration(overdue)}.", clientId, lastError),
                data: data);
        }

        if (licenseInfo?.Is_license_valid is false) {
            return HealthCheckResult.Unhealthy(
                AppendContext("SLASCONE reports that the license is not valid.", clientId, lastError),
                data: data);
        }

        if (activationStatus is LicenseActivationStatus.ActivationFailed) {
            return HealthCheckResult.Unhealthy(
                AppendContext("SLASCONE license activation failed for this deployment.", clientId, lastError),
                data: data);
        }

        if (heartbeatStatus is LicenseHeartbeatStatus.Failed) {
            return HealthCheckResult.Unhealthy(
                AppendContext("SLASCONE heartbeat failed and no valid floating session is currently available.", clientId, lastError),
                data: data);
        }

        if (sessionInfo?.Is_session_valid is false || lastOpenSessionAttemptStatus is LicenseSessionStatus.Closed) {
            return HealthCheckResult.Unhealthy(
                AppendContext("SLASCONE floating session is not valid, so no floating seat is assigned to this deployment.", clientId, lastError),
                data: data);
        }

        return HealthCheckResult.Unhealthy(
            AppendContext("SLASCONE licensing has not completed its first evaluation yet; this is the expected startup state until activation, heartbeat, and floating session checks finish.", clientId, lastError),
            data: data);
    }

    private static Dictionary<string, object> BuildHealthData(
        LicenseComplianceStatus complianceStatus,
        LicenseActivationStatus activationStatus,
        LicenseHeartbeatStatus heartbeatStatus,
        LicenseSessionStatus lastOpenSessionAttemptStatus,
        LicenseInfoDto? licenseInfo,
        SessionStatusDto? sessionInfo,
        LicensingOperationError? lastError,
        string clientId) {
        var data = new Dictionary<string, object> {
            [nameof(ILicensingService.ClientId)] = clientId,
            [nameof(ILicensingService.ComplianceStatus)] = complianceStatus.ToString(),
            [nameof(ILicensingService.ActivationStatus)] = activationStatus.ToString(),
            [nameof(ILicensingService.HeartbeatStatus)] = heartbeatStatus.ToString(),
            [nameof(ILicensingService.LastOpenSessionAttemptStatus)] = lastOpenSessionAttemptStatus.ToString()
        };

        if (licenseInfo is not null)
            data[nameof(LicenseInfoDto.Is_license_valid)] = licenseInfo.Is_license_valid;

        if (sessionInfo is not null) {
            data[nameof(SessionStatusDto.Is_session_valid)] = sessionInfo.Is_session_valid;
            data[nameof(SessionStatusDto.Max_open_session_count)] = sessionInfo.Max_open_session_count;

            if (sessionInfo.Session_valid_until.HasValue)
                data[nameof(SessionStatusDto.Session_valid_until)] = sessionInfo.Session_valid_until.Value;
        }

        if (lastError is not null) {
            data[nameof(ILicensingService.LastError)] = lastError;
            data[nameof(LicensingOperationError.Code)] = lastError.Code;
            data[nameof(LicensingOperationError.ErrorCode)] = lastError.ErrorCode.ToString();
        }

        return data;
    }

    private static bool IsSessionStillValid(SessionStatusDto? sessionInfo, DateTimeOffset utcNow)
        => sessionInfo?.Is_session_valid is true && sessionInfo.Session_valid_until > utcNow;

    private static bool IsSessionExpired(SessionStatusDto? sessionInfo, DateTimeOffset utcNow)
        => sessionInfo?.Session_valid_until <= utcNow;

    private static string AppendContext(string description, string clientId, LicensingOperationError? lastError) {
        var result = $"{description} Client id: {clientId}.";

        if (lastError is not null)
            result += $" SLASCONE error code: {lastError.Code} ({lastError.ErrorCode}).";
        else
            result += " SLASCONE error code: none.";

        return result;
    }

    private static string FormatTimestamp(DateTimeOffset? timestamp)
        => timestamp?.ToString("O") ?? "unknown";

    private static string FormatDuration(TimeSpan duration)
        => duration.ToString(@"d\.hh\:mm\:ss");

}
