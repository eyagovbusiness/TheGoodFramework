using HealthChecks.RabbitMQ;
using Microsoft.Extensions.Diagnostics.HealthChecks;
using TGF.CA.Infrastructure.Comm.RabbitMQ.Settings;

namespace TGF.CA.Infrastructure.Comm.RabbitMQ {
    public class CusomRabbitMQHealthCheck(IRabbitMQSettingsFactory aRabbitMQSettingsFactory) : IHealthCheck {
        private readonly Lazy<Task<RabbitMQHealthCheck>> _rabbitMQHealthCheck = new(GetRabbitMQHealthCheck(aRabbitMQSettingsFactory));

        public async Task<HealthCheckResult> CheckHealthAsync(HealthCheckContext aContext, CancellationToken aCancellationToken = default)
        => await (await _rabbitMQHealthCheck.Value)
            .CheckHealthAsync(aContext, aCancellationToken);

        private static async Task<RabbitMQHealthCheck> GetRabbitMQHealthCheck(IRabbitMQSettingsFactory aRabbitMQSettingsFactory) {
            var lRabbitMQSettings = await aRabbitMQSettingsFactory.GetRabbitMQSettingsAsync();
            var lRabbitMQHealthCheckOptions = new RabbitMQHealthCheckOptions() { ConnectionUri = new Uri(lRabbitMQSettings.GetConnectionString()) };
            return new RabbitMQHealthCheck(lRabbitMQHealthCheckOptions);
        }
    }

}
