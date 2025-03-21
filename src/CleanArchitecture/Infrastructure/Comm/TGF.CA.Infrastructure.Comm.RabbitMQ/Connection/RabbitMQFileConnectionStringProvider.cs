using Microsoft.Extensions.Configuration;
using TGF.CA.Infrastructure.Secrets.SecretsFiles;

namespace TGF.CA.Infrastructure.Comm.RabbitMQ.Connection;
/// <summary>
/// Provides the RabbitMQ connection string from the secrets file specified in the configuration.
/// </summary>
internal class RabbitMQFileConnectionStringProvider(IConfiguration configuration)
: IRabbitMQConnectionStringProvider {
    public async Task<string> GetConnectionString()
    => await SecretsFiles.GetSecretFromConfigAsync(configuration, InvariantConstants.ConfigurationKeys.SecretsFiles.SecretsFileNames.RabbitMQConnectionString);
}

