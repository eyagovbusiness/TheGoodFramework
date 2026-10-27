using HealthChecks.RabbitMQ;
using Microsoft.Extensions.Diagnostics.HealthChecks;
using RabbitMQ.Client;
using TGF.CA.Infrastructure.Comm.RabbitMQ.Connection;
using TGF.Common.Extensions;

namespace TGF.CA.Infrastructure.Comm.RabbitMQ;

/// <summary>
/// Custom RabbitMQ health check using a connection factory and retry logic.
/// </summary>
/// <remarks>
/// The underlying <see cref="RabbitMQHealthCheck"/> is bound to one specific <see cref="IConnection"/>, so it
/// is cached per connection instance rather than for the lifetime of this check. Caching it unconditionally
/// meant that once the bound connection died the check reported Unhealthy forever, which kept the service
/// permanently unready even after the broker and the connection had recovered.
/// The cached instance is still reused while the connection stays the same, so a probe does not open a new
/// connection on every request.
/// </remarks>
internal class CustomRabbitMQHealthCheck(IRabbitMQConnectionFactory aRabbitMQConnectionFactory, IRetryUtility retryUtility)
    : IHealthCheck {
    private readonly SemaphoreSlim _healthCheckLock = new(1, 1);
    private RabbitMQHealthCheck? _rabbitMQHealthCheck;
    private IConnection? _boundConnection;

    public async Task<HealthCheckResult> CheckHealthAsync(HealthCheckContext aContext, CancellationToken aCancellationToken = default) {
        try {
            var healthCheck = await GetRabbitMQHealthCheck(aRabbitMQConnectionFactory, retryUtility, aCancellationToken);
            var result = await healthCheck.CheckHealthAsync(aContext, aCancellationToken);

            // The third-party check attaches the broker exception to non-healthy results; only the status is kept because
            // health infrastructure logs attached exceptions and descriptions and broker messages can carry connection details.
            return result.Status == HealthStatus.Healthy
                ? HealthCheckResult.Healthy()
                : new HealthCheckResult(result.Status, "RabbitMQ is unavailable.");
        }
        catch (Exception) {
            return HealthCheckResult.Unhealthy("Failed to initialize RabbitMQ health check.");
        }
    }

    private async Task<RabbitMQHealthCheck> GetRabbitMQHealthCheck(IRabbitMQConnectionFactory aRabbitMQConnectionFactory, IRetryUtility retryUtility, CancellationToken aCancellationToken) {
        // Execute the retry utility to actually return the IConnection object. The factory reconnects when the
        // cached connection is dead, so a later attempt can genuinely return a different, healthy connection.
        // The budget is deliberately small: a readiness probe must fail fast rather than occupy the prober for
        // tens of seconds while the broker is down. Recovery does not depend on this retry, it depends on the
        // factory reconnecting and on this check rebinding to the new connection.
        IConnection connection = await retryUtility.ExecuteWithRetryAsync(
            async () => await aRabbitMQConnectionFactory.GetConnectionAsync(),
            conn => conn == null || !conn.IsOpen, // Condition to retry if true
            aMaxRetries: 3,
            aDelayMilliseconds: 200,
            aCancellationToken);

        var cached = _rabbitMQHealthCheck;
        if (cached is not null && ReferenceEquals(_boundConnection, connection))
            return cached;

        await _healthCheckLock.WaitAsync(aCancellationToken).ConfigureAwait(false);
        try {
            if (_rabbitMQHealthCheck is not null && ReferenceEquals(_boundConnection, connection))
                return _rabbitMQHealthCheck;

            // Map the connection to the AspNetCore.HealthChecks.RabbitMQ options
            var options = new RabbitMQHealthCheckOptions {
                Connection = connection
            };

            _rabbitMQHealthCheck = new RabbitMQHealthCheck(options);
            _boundConnection = connection;
            return _rabbitMQHealthCheck;
        }
        finally {
            _healthCheckLock.Release();
        }
    }
}
