using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using TGF.CA.Application;
using TGF.CA.Infrastructure.Comm.RabbitMQ;
using TGF.CA.Infrastructure.Discovery;

namespace TGF.CA.Infrastructure.Comm.RabbitMQ.Settings {
    /// <summary>
    /// <see cref="IRabbitMQSettingsFactory"/> implementation.
    /// </summary>
    internal class RabbitMQSettingsFactory : IRabbitMQSettingsFactory {
        private readonly IServiceDiscovery _serviceDiscovery;
        private readonly ISecretsManager _secretsManager;
        private readonly IConfiguration _configuration;
        public RabbitMQSettingsFactory(IServiceDiscovery aServiceDiscovery, ISecretsManager aSecretsManager, IConfiguration aConfiguration) {
            _serviceDiscovery = aServiceDiscovery;
            _secretsManager = aSecretsManager;
            _configuration = aConfiguration;
        }

        #region IRabbitMQSettingsFactory
        public async Task<RabbitMQSettings> GetRabbitMQSettingsAsync() {
            var lRabbitMQSettings = GetRabbitMQBusSettings(_configuration);
            lRabbitMQSettings.SetCredentials(await GetRabbitMqSecretCredentials());
            lRabbitMQSettings.SetHostName(await GetRabbitMQHostName());
            return lRabbitMQSettings;
        }
        #endregion

        private static RabbitMQSettings GetRabbitMQBusSettings(IConfiguration aConfiguration) {
            var lRabbitMQSettings = new RabbitMQSettings();
            aConfiguration.GetSection("Bus:RabbitMQ").Bind(lRabbitMQSettings);
            return lRabbitMQSettings;
        }

        private async Task<RabbitMQCredentials> GetRabbitMqSecretCredentials()
            => await _secretsManager.Get<RabbitMQCredentials>("rabbitmq");

        private async Task<string> GetRabbitMQHostName(CancellationToken aCancellationToken = default)
            => await _serviceDiscovery.GetFullAddress(InfraServicesRegistry.RabbitMQMessageBroker, aCancellationToken)!;

    }
}
