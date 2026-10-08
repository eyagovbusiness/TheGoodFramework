using HealthChecks.RabbitMQ;
using Microsoft.Extensions.Diagnostics.HealthChecks;
using RabbitMQ.Client;
using TGF.CA.Infrastructure.Comm.RabbitMQ.Connection;
using TGF.Common.Extensions;

namespace TGF.CA.Infrastructure.Comm.RabbitMQ;

/// <summary>
/// Custom RabbitMQ health check using a connection factory and retry logic.
/// </summary>
internal class CustomRabbitMQHealthCheck(IRabbitMQConnectionFactory aRabbitMQConnectionFactory, IRetryUtility retryUtility)
    : IHealthCheck {
    // We keep the Lazy initialization, but Note: RabbitMQ connections 
    // should usually be managed carefully to avoid leaking during health checks.
    private readonly Lazy<Task<RabbitMQHealthCheck>> _rabbitMQHealthCheck = new(() => GetRabbitMQHealthCheck(aRabbitMQConnectionFactory, retryUtility));

    public async Task<HealthCheckResult> CheckHealthAsync(HealthCheckContext aContext, CancellationToken aCancellationToken = default) {
        try {
            var healthCheck = await _rabbitMQHealthCheck.Value;
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

    private static async Task<RabbitMQHealthCheck> GetRabbitMQHealthCheck(IRabbitMQConnectionFactory aRabbitMQConnectionFactory, IRetryUtility retryUtility) {
        // Execute the retry utility to actually return the IConnection object
        IConnection connection = await retryUtility.ExecuteWithRetryAsync(
            async () => await aRabbitMQConnectionFactory.GetConnectionAsync(),
            conn => conn == null || !conn.IsOpen, // Condition to retry if true
            aMaxRetries: 10,
            aDelayMilliseconds: 500);

        // Map the connection to the AspNetCore.HealthChecks.RabbitMQ options
        var options = new RabbitMQHealthCheckOptions {
            Connection = connection
        };

        return new RabbitMQHealthCheck(options);
    }
}