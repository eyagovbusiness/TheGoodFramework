using HealthChecks.RabbitMQ;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Diagnostics.HealthChecks;
using TGF.CA.Infrastructure.Comm.RabbitMQ.Connection;

namespace TGF.CA.Infrastructure.Comm.RabbitMQ;

/// <summary>
/// Custom RabbitMQ health check, which uses the <see cref="RabbitMQHealthCheck"/> to check the health of the RabbitMQ connection, but also uses the <see cref="IRabbitMQConnectionFactory"/> to get the connection and the <see cref="IConfiguration"/> to get the configuration.
/// </summary>
/// <param name="aRabbitMQConnectionFactory"><see cref="IRabbitMQConnectionFactory"/> to create a new instance of the RabbitMQ connection.</param>
/// <param name="configuration"><see cref="IConfiguration"/> required to get the configuration.</param>
/// <remarks>Designed to be registered as a Singleton instance. Add it to DI as a Singleton before registering it as a health check.</remarks>
internal class CustomRabbitMQHealthCheck(IRabbitMQConnectionFactory aRabbitMQConnectionFactory, IConfiguration configuration)
: IHealthCheck {
    private readonly Lazy<Task<RabbitMQHealthCheck>> _rabbitMQHealthCheck = new(() => GetRabbitMQHealthCheck(aRabbitMQConnectionFactory, configuration));

    public async Task<HealthCheckResult> CheckHealthAsync(HealthCheckContext aContext, CancellationToken aCancellationToken = default)
    => await (await _rabbitMQHealthCheck.Value)
        .CheckHealthAsync(aContext, aCancellationToken);

    private static async Task<RabbitMQHealthCheck> GetRabbitMQHealthCheck(IRabbitMQConnectionFactory aRabbitMQConnectionFactory, IConfiguration configuration) {
        var lRabbitMQConnection = await aRabbitMQConnectionFactory.GetConnectionAsync();
        var lRabbitMQHealthCheckOptions = new RabbitMQHealthCheckOptions() { Connection = lRabbitMQConnection };
        return new RabbitMQHealthCheck(lRabbitMQHealthCheckOptions);
    }
}

