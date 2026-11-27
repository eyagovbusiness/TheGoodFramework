using Microsoft.Extensions.Logging;
using TGF.CA.Infrastructure.Licensing.Slascone.Contracts;

namespace TGF.CA.Infrastructure.Licensing.Slascone.Services;

/// <summary>
/// Coordinates recovery actions and guidance based on license activation failure reasons.
/// </summary>
/// <remarks>
/// <para>
/// This service maps <see cref="LicenseActivationFailureReason"/> values to actionable recovery steps,
/// user guidance, and appropriate logging levels. It enables:
/// </para>
/// 
/// <list type="number">
/// <item><description>
///   <strong>Diagnostic Guidance:</strong> Provides developers and operators with clear actions based on failure type.
/// </description></item>
/// <item><description>
///   <strong>User Communication:</strong> Supplies user-friendly error messages and recovery instructions.
/// </description></item>
/// <item><description>
///   <strong>Alerting Strategy:</strong> Distinguishes between expected transient failures and actionable permanent failures.
/// </description></item>
/// <item><description>
///   <strong>Monitoring Integration:</strong> Enables health checks and alerting systems to respond appropriately.
/// </description></item>
/// </list>
/// </remarks>
public sealed class LicenseRecoveryCoordinator(ILogger<LicenseRecoveryCoordinator> logger) {

    /// <summary>
    /// Represents recovery guidance for a license activation failure.
    /// </summary>
    /// <param name="Reason">The underlying failure reason.</param>
    /// <param name="IsTransient">True if the failure may auto-recover with retry; false if manual intervention is required.</param>
    /// <param name="LogLevel">Recommended logging level for this failure type.</param>
    /// <param name="UserMessage">User-friendly guidance for this failure.</param>
    /// <param name="DiagnosticMessage">Detailed diagnostic message for operators/developers.</param>
    /// <param name="RecoverySteps">Ordered list of recovery steps to try.</param>
    public sealed record RecoveryGuidance(
        LicenseActivationFailureReason Reason,
        bool IsTransient,
        LogLevel LogLevel,
        string UserMessage,
        string DiagnosticMessage,
        string[] RecoverySteps
    );

    /// <summary>
    /// Gets recovery guidance for the specified failure reason.
    /// </summary>
    /// <param name="reason">The failure reason to analyze.</param>
    /// <returns>Detailed recovery guidance including user and diagnostic messages.</returns>
    public RecoveryGuidance GetRecoveryGuidance(LicenseActivationFailureReason reason) {
        return reason switch {
            LicenseActivationFailureReason.TransientNetworkError => new(
                Reason: reason,
                IsTransient: true,
                LogLevel: LogLevel.Warning,
                UserMessage: "Network error during license activation. The system will automatically retry. Please ensure internet connectivity.",
                DiagnosticMessage: "Transient network failure (timeout, connection refused, or temporary service unavailability) detected during activation. The service will retry with exponential backoff.",
                RecoverySteps: new[] {
                    "Verify network connectivity and firewall rules",
                    "Check SLASCONE service status",
                    "Wait for automatic retry (exponential backoff: 2s, 4s, 8s, 16s, 32s)",
                    "If retries exhaust, restart the application",
                }
            ),

            LicenseActivationFailureReason.LicenseKeyMismatch => new(
                Reason: reason,
                IsTransient: false,
                LogLevel: LogLevel.Error,
                UserMessage: "License key mismatch. The secret file does not match the license on the server. Please contact support.",
                DiagnosticMessage: "The license key in the secret file does not match the response from SLASCONE. This indicates: (1) secret file was modified after activation, (2) license was changed server-side without updating the secret file, or (3) multiple keys are configured.",
                RecoverySteps: new[] {
                    "Verify the secret file contains the correct license key",
                    "Compare with the key recorded during initial activation",
                    "If key was rotated server-side, update the secret file with the new key",
                    "Restart the application to trigger re-activation",
                    "If issue persists, contact support with activation logs",
                }
            ),

            LicenseActivationFailureReason.DeviceAlreadyActivated => new(
                Reason: reason,
                IsTransient: false,
                LogLevel: LogLevel.Error,
                UserMessage: "This device is already activated with a different license key. Use the original key or contact support to revoke the previous activation.",
                DiagnosticMessage: "SLASCONE rejected activation because the device is already registered with a different license key. This can occur when: (1) attempting to activate with a new key on a device that was previously activated, (2) multiple nodes in a cluster use different keys, or (3) the device was manually re-registered server-side.",
                RecoverySteps: new[] {
                    "Use the original license key that was used for initial activation",
                    "OR contact support to revoke the previous device activation",
                    "After revocation, the device can be re-activated with the new key",
                    "For cluster scenarios: ensure all nodes use the same license key",
                }
            ),

            LicenseActivationFailureReason.ServerDeviceMismatch => new(
                Reason: reason,
                IsTransient: false,
                LogLevel: LogLevel.Error,
                UserMessage: "Server and device license state are out of sync. Restart the application or contact support.",
                DiagnosticMessage: "The SLASCONE server and local device state are inconsistent. This can occur when: (1) device was deactivated server-side, (2) session state was externally cleared, (3) concurrent operations on the same device without proper serialization, or (4) clock skew between client and server.",
                RecoverySteps: new[] {
                    "Verify system clock is synchronized (NTP)",
                    "Check that only one instance of the application runs on this device",
                    "Restart the application to resync state",
                    "If issue persists after restart, check SLASCONE device registration status",
                    "Contact support with device ID and SLASCONE logs",
                }
            ),

            LicenseActivationFailureReason.LicenseRevoked => new(
                Reason: reason,
                IsTransient: false,
                LogLevel: LogLevel.Error,
                UserMessage: "The license has been revoked or is no longer valid. Please contact support.",
                DiagnosticMessage: "SLASCONE rejected the license key because it has been revoked, is expired, or is no longer valid. This requires administrative action on the license.",
                RecoverySteps: new[] {
                    "Contact your license administrator or support team",
                    "Verify the license is still active in SLASCONE portal",
                    "Check license expiration date",
                    "Request license renewal or reactivation if applicable",
                    "Provide support with device ID and license key (last 4 chars only)",
                }
            ),

            LicenseActivationFailureReason.InvalidCredentials => new(
                Reason: reason,
                IsTransient: false,
                LogLevel: LogLevel.Error,
                UserMessage: "Invalid license credentials. The license key format or content is incorrect.",
                DiagnosticMessage: "The license key supplied is invalid, malformed, or incomplete. This can occur when: (1) key was truncated or corrupted, (2) wrong key type was provided, (3) key is for a different product.",
                RecoverySteps: new[] {
                    "Verify the license key format is correct",
                    "Check for truncation or corruption in the secret file",
                    "Confirm the key is for the correct product",
                    "Re-download the license key from SLASCONE portal",
                    "Update the secret file and restart the application",
                    "Contact support if the issue persists",
                }
            ),

            _ => new(
                Reason: reason,
                IsTransient: false,
                LogLevel: LogLevel.Error,
                UserMessage: "An unknown license activation error occurred. Please restart the application or contact support.",
                DiagnosticMessage: "The failure reason is unknown or not yet categorized. Check application logs for additional details.",
                RecoverySteps: new[] {
                    "Review application logs for detailed error information",
                    "Restart the application",
                    "If issue persists, contact support with full logs",
                }
            )
        };
    }

    /// <summary>
    /// Logs recovery guidance at the appropriate level and returns the guidance for further use.
    /// </summary>
    /// <param name="reason">The failure reason to log and analyze.</param>
    /// <returns>Detailed recovery guidance.</returns>
    public RecoveryGuidance LogAndGetRecoveryGuidance(LicenseActivationFailureReason reason) {
        var guidance = GetRecoveryGuidance(reason);

        logger.Log(
            guidance.LogLevel,
            "[LICENSE] Activation failed: {Reason}. User message: {UserMessage} Diagnostic: {DiagnosticMessage}",
            reason,
            guidance.UserMessage,
            guidance.DiagnosticMessage
        );

        logger.Log(
            guidance.LogLevel,
            "[LICENSE] Recovery steps for {Reason}: {RecoverySteps}",
            reason,
            string.Join(" → ", guidance.RecoverySteps)
        );

        return guidance;
    }

    /// <summary>
    /// Determines if a failure is likely to resolve with automatic retry.
    /// </summary>
    /// <param name="reason">The failure reason to check.</param>
    /// <returns>True if the failure is transient and may auto-recover; false if manual intervention is likely needed.</returns>
    public bool IsTransientFailure(LicenseActivationFailureReason reason) {
        return reason == LicenseActivationFailureReason.TransientNetworkError;
    }

    /// <summary>
    /// Gets a brief summary suitable for health check status or alert titles.
    /// </summary>
    /// <param name="reason">The failure reason to summarize.</param>
    /// <returns>Brief, operator-friendly summary.</returns>
    public string GetSummary(LicenseActivationFailureReason reason) {
        return reason switch {
            LicenseActivationFailureReason.TransientNetworkError => "Network error (auto-retrying)",
            LicenseActivationFailureReason.LicenseKeyMismatch => "License key mismatch",
            LicenseActivationFailureReason.DeviceAlreadyActivated => "Device already activated with different key",
            LicenseActivationFailureReason.ServerDeviceMismatch => "Server-device state mismatch",
            LicenseActivationFailureReason.LicenseRevoked => "License revoked or invalid",
            LicenseActivationFailureReason.InvalidCredentials => "Invalid license credentials",
            _ => "Unknown activation failure"
        };
    }
}
