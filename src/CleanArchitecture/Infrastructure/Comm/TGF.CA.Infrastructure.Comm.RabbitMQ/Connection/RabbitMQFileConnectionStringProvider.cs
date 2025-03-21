using Microsoft.Extensions.Configuration;
using TGF.CA.Infrastructure.Secrets.SecretsFiles;

namespace TGF.CA.Infrastructure.Comm.RabbitMQ.Connection;

internal class RabbitMQFileConnectionStringProvider(IConfiguration configuration)
: IRabbitMQConnectionStringProvider {
    public async Task<string> GetConnectionString()
    => await SecretsFiles.GetSecretFromConfigAsync(configuration, InvariantConstants.ConfigurationKeys.SecretsFiles.SecretsFileNames.RabbitMQConnectionString);
}

