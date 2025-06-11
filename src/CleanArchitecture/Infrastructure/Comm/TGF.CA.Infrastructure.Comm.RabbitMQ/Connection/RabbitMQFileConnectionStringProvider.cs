using TGF.CA.Infrastructure.Secrets.SecretsFiles;

namespace TGF.CA.Infrastructure.Comm.RabbitMQ.Connection;

internal class RabbitMQFileConnectionStringProvider(ISecretFilesService secretFilesService)
: IRabbitMQConnectionStringProvider {
    public async Task<string> GetConnectionString()
    => await secretFilesService.GetSecretFromConfigAsync(InvariantConstants.ConfigurationKeys.SecretsFiles.SecretsFileNames.RabbitMQConnectionString);
}

