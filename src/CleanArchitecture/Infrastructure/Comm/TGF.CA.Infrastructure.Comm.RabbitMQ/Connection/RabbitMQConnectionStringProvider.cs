using TGF.CA.Application;
using TGF.CA.Domain.ExternalContracts;
using TGF.CA.Infrastructure.Discovery;

namespace TGF.CA.Infrastructure.Comm.RabbitMQ.Connection;
/// <summary>
/// Provides the RabbitMQ connection string from the secrets manager and discovery service.
/// </summary>
internal class RabbitMQConnectionStringProvider(IServiceDiscovery serviceDiscovery, ISecretsManager secretsManager)
: IRabbitMQConnectionStringProvider {

    private Lazy<Task<string>> Hostname => new(GetRabbitMQHostName);
    private Lazy<Task<RabbitMQCredentials>> Credentials => new(GetRabbitMqSecretCredentials);

    private async Task<RabbitMQCredentials> GetRabbitMqSecretCredentials()
    => await secretsManager.Get<RabbitMQCredentials>("rabbitmq");

    private async Task<string> GetRabbitMQHostName()
    => await serviceDiscovery.GetFullAddress(InfraServicesRegistry.RabbitMQMessageBroker)!;
    public async Task<string> GetConnectionString() {
        var hostname = await Hostname.Value;
        var credentials = await Credentials.Value;
        return string.IsNullOrEmpty(hostname) || credentials == null || string.IsNullOrEmpty(credentials.Username) || string.IsNullOrEmpty(credentials.Password)
            ? throw new InvalidOperationException("[ERROR] Error building the connection string: connection secrets were incomplete or not set")
            : $"amqp://{credentials.Username}:{credentials.Password}@{hostname}";
    }
    internal record RabbitMQCredentials : IBasicCredentials {
        public string Username { get; set; } = default!;
        public string Password { get; set; } = default!;
    }
}

