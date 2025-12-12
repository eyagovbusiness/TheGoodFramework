using Microsoft.Extensions.Diagnostics.HealthChecks;

namespace TGF.CA.Infrastructure.Licensing.Slascone.Services;

/// <summary>
/// Provides a thread-safe cache for the last Slascone licensing health check result.
/// </summary>
internal class SlasconeLicensingHealthCheckCacheService {
    private HealthCheckResult? _lastResult;
    private readonly object _lock = new();

    /// <summary>
    /// Stores the latest health check result.
    /// </summary>
    public void SetLastResult(HealthCheckResult result) {
        lock (_lock) {
            _lastResult = result;
        }
    }

    /// <summary>
    /// Retrieves the last cached health check result, or null if none exists.
    /// </summary>
    public HealthCheckResult? GetLastResult() {
        lock (_lock) {
            return _lastResult;
        }
    }
}

