using Microsoft.Extensions.Diagnostics.HealthChecks;
using Slascone.Client;
using TGF.CA.Infrastructure.Licensing.Slascone;
using TGF.CA.Infrastructure.Licensing.Slascone.Contracts;

namespace TGF.CA.Infrastructure.Licensing.Tests.Unit.Infrastructure;

public class SlasconeLicensingHealthCheckTests {
    private const string ClientId = "omicsflow-client-42";
    private const string FakeLicenseKey = "11111111-2222-3333-4444-555555555555";
    private static readonly DateTimeOffset UtcNow = new(2026, 9, 11, 10, 0, 0, TimeSpan.Zero);

    [Fact]
    public void GetLicensingHealthCheckResult_ShouldReturnHealthyWhenCompliant() {
        var sessionValidUntil = UtcNow.AddHours(6);

        var result = GetResult(
            LicenseComplianceStatus.Compliant,
            LicenseActivationStatus.Activated,
            LicenseHeartbeatStatus.Success,
            LicenseSessionStatus.Opened,
            CreateLicenseInfo(),
            CreateSessionInfo(sessionValidUntil));

        Assert.Equal(HealthStatus.Healthy, result.Status);
        Assert.Contains("compliant", result.Description);
        Assert.Contains(sessionValidUntil.ToString("O"), result.Description);
        Assert.Contains(ClientId, result.Description);
    }

    [Fact]
    public void GetLicensingHealthCheckResult_ShouldReportSeatExhaustionWithSessionLimitForExceededAllowedConnections() {
        var result = GetResult(
            lastOpenSessionAttemptStatus: LicenseSessionStatus.OpenFailed,
            sessionInfo: CreateSessionInfo(UtcNow.AddHours(1), maxOpenSessionCount: 3),
            lastError: CreateError(1007, SlasconeApiErrorCode.EXCEEDED_ALLOWED_CONNECTIONS));

        Assert.Equal(HealthStatus.Unhealthy, result.Status);
        Assert.Contains("floating license seats exhausted", result.Description);
        Assert.Contains("concurrent floating sessions/seats", result.Description);
        Assert.Contains("Max_open_session_count: 3", result.Description);
        Assert.Contains("1007", result.Description);
        Assert.Contains(ClientId, result.Description);
    }

    [Fact]
    public void GetLicensingHealthCheckResult_ShouldReportSeatExhaustionWithoutSessionLimitWhenSessionInfoIsMissing() {
        var result = GetResult(
            lastOpenSessionAttemptStatus: LicenseSessionStatus.OpenFailed,
            sessionInfo: null,
            lastError: CreateError(1007, SlasconeApiErrorCode.EXCEEDED_ALLOWED_CONNECTIONS));

        Assert.Equal(HealthStatus.Unhealthy, result.Status);
        Assert.Contains("floating license seats exhausted", result.Description);
        Assert.Contains("concurrent floating sessions/seats", result.Description);
        Assert.DoesNotContain("Max_open_session_count", result.Description);
        Assert.Contains("1007", result.Description);
    }

    [Fact]
    public void GetLicensingHealthCheckResult_ShouldReportUnknownClientWithClientId() {
        var result = GetResult(lastError: CreateError(2006, SlasconeApiErrorCode.UNKNOWN_CLIENT));

        Assert.Equal(HealthStatus.Unhealthy, result.Status);
        Assert.Contains("client id is not activated", result.Description);
        Assert.Contains(ClientId, result.Description);
        Assert.Contains("2006", result.Description);
    }

    [Fact]
    public void GetLicensingHealthCheckResult_ShouldReportTokenAlreadyAssigned() {
        var result = GetResult(lastError: CreateError(2001, SlasconeApiErrorCode.TOKEN_ALREADY_ASSINGED));

        Assert.Equal(HealthStatus.Unhealthy, result.Status);
        Assert.Contains("token is already assigned", result.Description);
        Assert.Contains("2001", result.Description);
    }

    [Fact]
    public void GetLicensingHealthCheckResult_ShouldReportExpiredKey() {
        var result = GetResult(lastError: CreateError(1001, SlasconeApiErrorCode.EXPIRED_KEY));

        Assert.Equal(HealthStatus.Unhealthy, result.Status);
        Assert.Contains("license key has expired", result.Description);
        Assert.Contains("1001", result.Description);
    }

    [Fact]
    public void GetLicensingHealthCheckResult_ShouldReportInvalidKey() {
        var result = GetResult(lastError: CreateError(1000, SlasconeApiErrorCode.INVALID_KEY));

        Assert.Equal(HealthStatus.Unhealthy, result.Status);
        Assert.Contains("license key is invalid", result.Description);
        Assert.Contains("1000", result.Description);
    }

    [Fact]
    public void GetLicensingHealthCheckResult_ShouldReportNotActivated() {
        var result = GetResult(lastError: CreateError(1002, SlasconeApiErrorCode.NOT_ACTIVATED));

        Assert.Equal(HealthStatus.Unhealthy, result.Status);
        Assert.Contains("has not been activated", result.Description);
        Assert.Contains("1002", result.Description);
    }

    [Fact]
    public void GetLicensingHealthCheckResult_ShouldReportNonCompliantVersion() {
        var result = GetResult(lastError: CreateError(1003, SlasconeApiErrorCode.NON_COMPLIANT_VERSION));

        Assert.Equal(HealthStatus.Unhealthy, result.Status);
        Assert.Contains("software version is not compliant", result.Description);
        Assert.Contains("1003", result.Description);
    }

    [Fact]
    public void GetLicensingHealthCheckResult_ShouldDistinguishOpenSessionFailureFromHeartbeatFailure() {
        var openSessionFailure = GetResult(
            heartbeatStatus: LicenseHeartbeatStatus.Success,
            lastOpenSessionAttemptStatus: LicenseSessionStatus.OpenFailed);
        var heartbeatFailure = GetResult(heartbeatStatus: LicenseHeartbeatStatus.Failed);

        Assert.Equal(HealthStatus.Unhealthy, openSessionFailure.Status);
        Assert.Equal(HealthStatus.Unhealthy, heartbeatFailure.Status);
        Assert.Contains("heartbeat succeeded", openSessionFailure.Description);
        Assert.Contains("opening or renewing the floating session failed", openSessionFailure.Description);
        Assert.Contains("heartbeat failed", heartbeatFailure.Description);
        Assert.NotEqual(openSessionFailure.Description, heartbeatFailure.Description);
    }

    [Fact]
    public void GetLicensingHealthCheckResult_ShouldReturnDegradedWhenHeartbeatFailsButSessionIsStillValid() {
        var sessionValidUntil = UtcNow.AddMinutes(30);

        var result = GetResult(
            heartbeatStatus: LicenseHeartbeatStatus.Failed,
            lastOpenSessionAttemptStatus: LicenseSessionStatus.Opened,
            sessionInfo: CreateSessionInfo(sessionValidUntil));

        Assert.Equal(HealthStatus.Degraded, result.Status);
        Assert.Contains("heartbeat failed", result.Description);
        Assert.Contains("still valid", result.Description);
        Assert.Contains(sessionValidUntil.ToString("O"), result.Description);
    }

    [Fact]
    public void GetLicensingHealthCheckResult_ShouldReportExpiredPreviouslyOpenedSessionUsingInjectedTime() {
        var sessionValidUntil = UtcNow.AddMinutes(-15);

        var result = GetResult(
            lastOpenSessionAttemptStatus: LicenseSessionStatus.Opened,
            sessionInfo: CreateSessionInfo(sessionValidUntil));

        Assert.Equal(HealthStatus.Unhealthy, result.Status);
        Assert.Contains("previously opened", result.Description);
        Assert.Contains("renewal failed", result.Description);
        Assert.Contains("overdue", result.Description);
        Assert.Contains(sessionValidUntil.ToString("O"), result.Description);
    }

    [Fact]
    public void GetLicensingHealthCheckResult_ShouldReturnDegradedWhenCloseSessionFails() {
        var result = GetResult(lastOpenSessionAttemptStatus: LicenseSessionStatus.CloseFailed);

        Assert.Equal(HealthStatus.Degraded, result.Status);
        Assert.Contains("close failed", result.Description);
        Assert.Contains("seat may not be freed", result.Description);
        Assert.Contains("server-side session expiry", result.Description);
    }

    [Fact]
    public void GetLicensingHealthCheckResult_ShouldReportNetworkClassErrorWithoutClaimingLicenseRejection() {
        var result = GetResult(lastError: CreateError(9998, SlasconeApiErrorCode.API_CRASH));

        Assert.Equal(HealthStatus.Unhealthy, result.Status);
        Assert.Contains("API is unreachable", result.Description);
        Assert.Contains("distinct from a licensing violation", result.Description);
        Assert.Contains("9998", result.Description);
        Assert.DoesNotContain("rejected the license", result.Description);
    }

    [Fact]
    public void GetLicensingHealthCheckResult_ShouldReportStartupStateWhenNothingHasBeenEvaluatedYet() {
        var result = GetResult(
            activationStatus: LicenseActivationStatus.Unassigned,
            heartbeatStatus: LicenseHeartbeatStatus.None,
            lastOpenSessionAttemptStatus: LicenseSessionStatus.None,
            licenseInfo: null,
            sessionInfo: null,
            lastError: null);

        Assert.Equal(HealthStatus.Unhealthy, result.Status);
        Assert.Contains("has not completed its first evaluation yet", result.Description);
        Assert.Contains("expected startup state", result.Description);
        Assert.DoesNotContain("violation", result.Description);
    }

    [Theory]
    [MemberData(nameof(AllOutcomeDescriptions))]
    public void GetLicensingHealthCheckResult_ShouldAlwaysReturnSingleLineDescription(string description) {
        Assert.DoesNotContain("\n", description);
        Assert.DoesNotContain("\r", description);
    }

    [Fact]
    public void GetLicensingHealthCheckResult_ShouldNeverIncludeLicenseKeyInDescription() {
        var result = GetResult(
            licenseInfo: CreateLicenseInfo(licenseKey: FakeLicenseKey),
            lastError: CreateError(1007, SlasconeApiErrorCode.EXCEEDED_ALLOWED_CONNECTIONS));

        Assert.DoesNotContain(FakeLicenseKey, result.Description);
    }

    public static IEnumerable<object[]> AllOutcomeDescriptions() {
        foreach (var result in CreateAllOutcomes())
            yield return [result.Description ?? string.Empty];
    }

    private static IEnumerable<HealthCheckResult> CreateAllOutcomes() {
        yield return GetResult(
            LicenseComplianceStatus.Compliant,
            LicenseActivationStatus.Activated,
            LicenseHeartbeatStatus.Success,
            LicenseSessionStatus.Opened,
            CreateLicenseInfo(),
            CreateSessionInfo(UtcNow.AddHours(6)));
        yield return GetResult(lastOpenSessionAttemptStatus: LicenseSessionStatus.OpenFailed, sessionInfo: CreateSessionInfo(UtcNow.AddHours(1), maxOpenSessionCount: 3), lastError: CreateError(1007, SlasconeApiErrorCode.EXCEEDED_ALLOWED_CONNECTIONS));
        yield return GetResult(lastOpenSessionAttemptStatus: LicenseSessionStatus.OpenFailed, sessionInfo: null, lastError: CreateError(1007, SlasconeApiErrorCode.EXCEEDED_ALLOWED_CONNECTIONS));
        yield return GetResult(lastError: CreateError(2006, SlasconeApiErrorCode.UNKNOWN_CLIENT));
        yield return GetResult(lastError: CreateError(2001, SlasconeApiErrorCode.TOKEN_ALREADY_ASSINGED));
        yield return GetResult(lastError: CreateError(1001, SlasconeApiErrorCode.EXPIRED_KEY));
        yield return GetResult(lastError: CreateError(1000, SlasconeApiErrorCode.INVALID_KEY));
        yield return GetResult(lastError: CreateError(1002, SlasconeApiErrorCode.NOT_ACTIVATED));
        yield return GetResult(lastError: CreateError(1003, SlasconeApiErrorCode.NON_COMPLIANT_VERSION));
        yield return GetResult(heartbeatStatus: LicenseHeartbeatStatus.Success, lastOpenSessionAttemptStatus: LicenseSessionStatus.OpenFailed);
        yield return GetResult(heartbeatStatus: LicenseHeartbeatStatus.Failed, lastOpenSessionAttemptStatus: LicenseSessionStatus.Opened, sessionInfo: CreateSessionInfo(UtcNow.AddMinutes(30)));
        yield return GetResult(lastOpenSessionAttemptStatus: LicenseSessionStatus.Opened, sessionInfo: CreateSessionInfo(UtcNow.AddMinutes(-15)));
        yield return GetResult(lastOpenSessionAttemptStatus: LicenseSessionStatus.CloseFailed);
        yield return GetResult(lastError: CreateError(9998, SlasconeApiErrorCode.API_CRASH));
        yield return GetResult(activationStatus: LicenseActivationStatus.Unassigned, heartbeatStatus: LicenseHeartbeatStatus.None, lastOpenSessionAttemptStatus: LicenseSessionStatus.None, licenseInfo: null, sessionInfo: null, lastError: null);
    }

    private static HealthCheckResult GetResult(
        LicenseComplianceStatus complianceStatus = LicenseComplianceStatus.NonCompliant,
        LicenseActivationStatus activationStatus = LicenseActivationStatus.Activated,
        LicenseHeartbeatStatus heartbeatStatus = LicenseHeartbeatStatus.Success,
        LicenseSessionStatus lastOpenSessionAttemptStatus = LicenseSessionStatus.None,
        LicenseInfoDto? licenseInfo = null,
        SessionStatusDto? sessionInfo = null,
        LicensingOperationError? lastError = null)
        => SlasconeLicensingHealthCheck.GetLicensingHealthCheckResult(
            complianceStatus,
            activationStatus,
            heartbeatStatus,
            lastOpenSessionAttemptStatus,
            licenseInfo ?? CreateLicenseInfo(),
            sessionInfo,
            lastError,
            ClientId,
            UtcNow);

    private static LicenseInfoDto CreateLicenseInfo(bool isLicenseValid = true, string? licenseKey = null)
        => new() { Is_license_valid = isLicenseValid, License_key = licenseKey };

    private static SessionStatusDto CreateSessionInfo(DateTimeOffset sessionValidUntil, int maxOpenSessionCount = 2)
        => new() {
            Is_session_valid = sessionValidUntil > UtcNow,
            Session_valid_until = sessionValidUntil,
            Max_open_session_count = maxOpenSessionCount
        };

    private static LicensingOperationError CreateError(int code, SlasconeApiErrorCode errorCode)
        => new("test operation", code, errorCode, "stubbed SLASCONE error", UtcNow);
}
