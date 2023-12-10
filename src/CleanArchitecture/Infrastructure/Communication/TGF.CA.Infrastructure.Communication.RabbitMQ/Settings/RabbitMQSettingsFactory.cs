using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using TGF.CA.Application;
using TGF.CA.Infrastructure.Discovery;

namespace TGF.CA.Infrastructure.Communication.RabbitMQ.Settings
{
    internal class RabbitMQSettingsFactory : IRabbitMQSettingsFactory
    {
        private readonly IServiceDiscovery _serviceDiscovery;
        private readonly ISecretsManager _secretsManager;
        private readonly IConfiguration _configuration;
        public RabbitMQSettingsFactory(IServiceDiscovery aServiceDiscovery, ISecretsManager aSecretsManager, IConfiguration aConfiguration)
        {
            _serviceDiscovery = aServiceDiscovery;
            _secretsManager = aSecretsManager;
            _configuration = aConfiguration;
        }
        public async Task<RabbitMQSettings> GetRabbitMQSettingsAsync()
        {
            var lRabbitMQSettings = GetRabbitMQBusSettings(_configuration);
            lRabbitMQSettings.SetCredentials(await GetRabbitMqSecretCredentials());
            lRabbitMQSettings.SetHostName(await GetRabbitMQHostName());
            return lRabbitMQSettings;
        }
        private static RabbitMQSettings GetRabbitMQBusSettings(IConfiguration configuration)
        {
            var rabbitMQSettings = new RabbitMQSettings();
            configuration.GetSection("Bus:RabbitMQ").Bind(rabbitMQSettings);
            return rabbitMQSettings;
        }

        private async Task<RabbitMQCredentials> GetRabbitMqSecretCredentials()
            => await _secretsManager.Get<RabbitMQCredentials>("rabbitmq");

        private async Task<string> GetRabbitMQHostName()
            => await _serviceDiscovery.GetFullAddress(InfraServicesRegistry.RabbitMQMessageBroker)!;

    }
}
