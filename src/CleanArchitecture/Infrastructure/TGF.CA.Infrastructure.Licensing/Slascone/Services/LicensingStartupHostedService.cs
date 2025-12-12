using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using TGF.CA.Infrastructure.Licensing.Slascone.Contracts;

namespace TGF.CA.Infrastructure.Licensing.Slascone.Services;

/// <summary>
/// Hosted service that manages the licensing startup process, including license activation, seat acquisition, and maintaining the license session through periodic heartbeats.
/// </summary>
internal sealed class LicensingStartupHostedService(
    ILicensingService licensingService,
    IOptions<SlasconeOptions> slasconeOptions,
    ILogger<LicensingStartupHostedService> logger
) : BackgroundService {

    private const double RenewalThreshold = 0.8; // 80% of session duration
    private static readonly TimeSpan MinimumRenewMargin = TimeSpan.FromSeconds(30);

    /// <summary>
    /// Executes the licensing startup process. It first attempts to ensure activation and acquire a license seat. If successful, it enters a loop to keep the license session open by sending periodic heartbeats.
    /// </summary>
    /// <remarks>The <see cref="SlasconeLicensingHealthCheck"/> with the <seealso cref="LicensingGateMiddleware"/> ensure that only if <seealso cref="ILicensingService.ComplianceStatus"/> is compliant the api can be used. </remarks>
    protected override async Task ExecuteAsync(CancellationToken cancellationToken) {
        logger.LogInformation("[LICENSE] Licensing startup service started. Beginning preflight seat acquisition...");
        await EnsureActivationAsync(cancellationToken);
        await EnsureFloatingLicenseSeatAsync(cancellationToken);
        logger.LogInformation("[LICENSE] Licensing startup service is stopping (KeepLicenseSessionOpenAsync loop ended).");
    }

    /// <summary>
    /// Cleans up the license session on application shutdown by attempting to close the session gracefully.
    /// </summary>
    public override async Task StopAsync(CancellationToken cancellationToken) {
        await licensingService.CloseSessionAsync();
        if(licensingService.LastOpenSessionAttemptStatus is LicenseSessionStatus.Closed) {
            logger.LogWarning("[LICENSE] Session closed successfully during shutdown.");
            return;
        }
        logger.LogError("[LICENSE] Failed to close session during shutdown, this seat will not be freed until the session expires or is manually released by the license server.");
    }

    #region Private Methods

    /// <summary>Ensures license activation and retries with exponential backoff until successful or cancellation.</summary>
    private async Task EnsureActivationAsync(CancellationToken cancellationToken) {
        var attempt = 0;
        while (!cancellationToken.IsCancellationRequested) {
            try {
                logger.LogInformation("[LICENSE] Ensuring license activation, attempt {Attempt}", attempt + 1);
                await licensingService.AddHeartbeatAsync();

                if (licensingService.HeartbeatStatus == LicenseHeartbeatStatus.Success) {
                    logger.LogInformation("[LICENSE] Device already activated. Skipping activation.");
                    return;
                }

                await licensingService.ActivateLicenseAsync();
                if (licensingService.ActivationStatus == LicenseActivationStatus.Activated) {
                    logger.LogInformation("[LICENSE] Activation completed successfully.");
                    return;
                }

                logger.LogWarning("[LICENSE] Activation attempt {Attempt} failed. Retrying...", attempt + 1);
            }
            catch (Exception ex) {
                logger.LogWarning(ex, "[LICENSE] Activation attempt {Attempt} failed; retrying...", attempt + 1);
            }

            await BackoffDelayAsync(attempt++, cancellationToken);
        }
    }

    /// <summary>Manages floating license sessions: sends periodic heartbeats, renews sessions before expiry,and attempts to reopen sessions when lost. </summary>
    private async Task EnsureFloatingLicenseSeatAsync(CancellationToken cancellationToken) {
        var attempt = 0;
        while (!cancellationToken.IsCancellationRequested) {
            try {
                await licensingService.AddHeartbeatAsync();

                if (!IsLicenseValid()) {
                    logger.LogError("[LICENSE] License invalid. Retrying after backoff.");
                    await BackoffDelayAsync(attempt++, cancellationToken);
                    continue;
                }

                if (!IsSessionValid()) {
                    await TryOpenSessionAsync(cancellationToken);
                    await BackoffDelayAsync(attempt++, cancellationToken);
                    continue;
                }

                attempt = 0; // reset after success

                var (renewAt, delayUntilRenewal) = CalculateRenewalTiming();
                await DelaySafe(delayUntilRenewal, cancellationToken);

                if (DateTimeOffset.UtcNow >= renewAt)
                    await RenewSessionAsync();
            }
            catch (TaskCanceledException) {
                break; // shutting down gracefully
            }
            catch (Exception ex) {
                logger.LogWarning(ex, "[LICENSE] Error in session loop; retrying after backoff.");
                await BackoffDelayAsync(attempt++, cancellationToken);
            }
        }
    }



    /// <summary>Checks if the license is currently valid.</summary>
    private bool IsLicenseValid() => licensingService.LicenseInfo?.Is_license_valid == true;
    /// <summary>Checks if the session is currently valid.</summary>
    private bool IsSessionValid() => licensingService.SessionInfo?.Is_session_valid == true;

    private static TimeSpan Backoff(int attempt, int initialSeconds, int maxSecconds) {
        var BackoffSeconds = Math.Min(maxSecconds, Math.Max(1, (int)Math.Pow(2, attempt) * Math.Max(1, initialSeconds)));
        return TimeSpan.FromSeconds(BackoffSeconds);
    }

    private async Task BackoffDelayAsync(int attempt, CancellationToken cancellationToken) {
        var delay = Backoff(attempt, slasconeOptions.Value.StartupOptions.InitialBackoffSeconds,
                                   slasconeOptions.Value.StartupOptions.MaxBackoffSeconds);
        logger.LogWarning("[LICENSE] Waiting {Delay} before retrying...", delay);
        await DelaySafe(delay, cancellationToken);
    }

    /// <summary>Attempts to open a session if none is valid.</summary>
    private async Task TryOpenSessionAsync(CancellationToken cancellationToken) {
        logger.LogWarning("[LICENSE] Session invalid. Attempting to open...");
        await licensingService.OpenSessionAsync();

        if (IsSessionValid()) {
            logger.LogInformation("[LICENSE] Session opened. Valid until {ValidUntil}.",
                licensingService.SessionInfo?.Session_valid_until);
        } else {
            logger.LogWarning("[LICENSE] Failed to open session. Will retry with backoff.");
            await DelaySafe(Backoff(0, slasconeOptions.Value.StartupOptions.InitialBackoffSeconds,
                                       slasconeOptions.Value.StartupOptions.MaxBackoffSeconds), cancellationToken);
        }
    }

    /// <summary>Calculates renewal timing and delay.</summary>
    private (DateTimeOffset renewAt, TimeSpan delay) CalculateRenewalTiming() {
        var validUntil = licensingService.SessionInfo?.Session_valid_until ?? DateTimeOffset.UtcNow;
        var createdAt = licensingService.SessionInfo?.Session_created_date ?? DateTimeOffset.UtcNow;
        var totalDuration = validUntil - createdAt;
        var renewAt = createdAt + TimeSpan.FromTicks((long)(totalDuration.Ticks * RenewalThreshold));
        var delay = renewAt - DateTimeOffset.UtcNow;
        return (renewAt, delay < MinimumRenewMargin ? MinimumRenewMargin : delay);
    }

    /// <summary>Renews the session by calling OpenSessionAsync again.</summary>
    private async Task RenewSessionAsync() {
        logger.LogInformation("[LICENSE] Renewing session before expiry...");
        await licensingService.OpenSessionAsync();

        if (IsSessionValid()) {
            logger.LogInformation("[LICENSE] Session renewed. New valid until {ValidUntil}.",
                licensingService.SessionInfo?.Session_valid_until);
        } else {
            logger.LogWarning("[LICENSE] Renewal failed. Seat may be lost.");
        }
    }

    /// <summary>Safe delay wrapper to handle cancellation gracefully.</summary>
    private static async Task DelaySafe(TimeSpan delay, CancellationToken cancellationToken) {
        if (delay < TimeSpan.Zero) delay = TimeSpan.Zero;
        await Task.Delay(delay, cancellationToken);
    }

    #endregion

}
