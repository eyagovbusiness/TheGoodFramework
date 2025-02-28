using HealthChecks.RabbitMQ;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Diagnostics.HealthChecks;
using TGF.CA.Infrastructure.Comm.RabbitMQ.Settings;

namespace TGF.CA.Infrastructure.Comm.RabbitMQ;

/// <summary>
/// Custom RabbitMQ health check, which uses the <see cref="RabbitMQHealthCheck"/> to check the health of the RabbitMQ connection, but also uses the <see cref="IRabbitMQSettingsFactory"/> to get the connection string and the <see cref="IConfiguration"/> to get the configuration.
/// </summary>
/// <param name="aRabbitMQSettingsFactory"><see cref="IRabbitMQSettingsFactory"/> to create a new instance fo the RabbitMQ settings.</param>
/// <param name="configuration"><see cref="IConfiguration"/> required to get the connection string.</param>
/// <remarks>Designed to be registered as a Singleton instance.Add it to DI as a Singleron before registering it as a healthcheck.</remarks>
public class CustomRabbitMQHealthCheck(IRabbitMQSettingsFactory aRabbitMQSettingsFactory, IConfiguration configuration)
: IHealthCheck {
    private readonly Lazy<Task<RabbitMQHealthCheck>> _rabbitMQHealthCheck = new(() => GetRabbitMQHealthCheck(aRabbitMQSettingsFactory, configuration));

    public async Task<HealthCheckResult> CheckHealthAsync(HealthCheckContext aContext, CancellationToken aCancellationToken = default)
    => await (await _rabbitMQHealthCheck.Value)
        .CheckHealthAsync(aContext, aCancellationToken);

    private static async Task<RabbitMQHealthCheck> GetRabbitMQHealthCheck(IRabbitMQSettingsFactory aRabbitMQSettingsFactory, IConfiguration configuration) {
        var lRabbitMQSettings = await aRabbitMQSettingsFactory.GetRabbitMQSettingsAsync();
        var lRabbitMQHealthCheckOptions = new RabbitMQHealthCheckOptions() { ConnectionUri = new Uri(lRabbitMQSettings.GetConnectionString(configuration)) };
        return new RabbitMQHealthCheck(lRabbitMQHealthCheckOptions);
    }
}

