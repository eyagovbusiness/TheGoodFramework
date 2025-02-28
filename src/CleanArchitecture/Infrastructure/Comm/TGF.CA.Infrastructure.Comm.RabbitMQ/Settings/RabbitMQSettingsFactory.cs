using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using TGF.CA.Application;
using TGF.CA.Infrastructure.Discovery;

namespace TGF.CA.Infrastructure.Comm.RabbitMQ.Settings;

/// <summary>
/// <see cref="IRabbitMQSettingsFactory"/> implementation.
/// </summary>
internal class RabbitMQSettingsFactory(IServiceDiscovery serviceDiscovery, ISecretsManager secretsManager, IConfiguration configuration)
    : IRabbitMQSettingsFactory {

    #region IRabbitMQSettingsFactory
    public async Task<RabbitMQSettings> GetRabbitMQSettingsAsync() {
        var lRabbitMQSettings = GetRabbitMQBusSettings(configuration);
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
    => await secretsManager.Get<RabbitMQCredentials>("rabbitmq");

    private async Task<string> GetRabbitMQHostName(CancellationToken aCancellationToken = default)
    => await serviceDiscovery.GetFullAddress(InfraServicesRegistry.RabbitMQMessageBroker, aCancellationToken)!;

}

