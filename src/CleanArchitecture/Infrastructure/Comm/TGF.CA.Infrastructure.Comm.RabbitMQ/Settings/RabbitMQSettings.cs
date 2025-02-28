using Microsoft.Extensions.Configuration;
using TGF.CA.Domain.ExternalContracts;
using TGF.CA.Infrastructure.Secrets.SecretsFiles;

namespace TGF.CA.Infrastructure.Comm.RabbitMQ.Settings;

public class RabbitMQSettings {
    public string? Hostname { get; private set; }
    public RabbitMQCredentials? Credentials { get; private set; }
    public PublisherSettings? Publisher { get; init; }
    public ConsumerSettings? Consumer { get; init; }
    /// <summary>
    /// Specifies whether to use the secrets manager and service discovery to get the connection string or to use the secrets file specified in the configuration.
    /// </summary>
    public bool UseSecretsManagerAndServiceDiscovery { get; init; }

    public void SetCredentials(RabbitMQCredentials credentials)
    => Credentials = credentials;

    public void SetHostName(string hostname)
    => Hostname = hostname;

    /// <summary>
    /// Get the connection string for RabbitMQ, either from the secrets manager or from the secrets file specified in the configuration.
    /// </summary>
    /// <param name="configuration"></param>
    /// <returns></returns>
    /// <exception cref="InvalidOperationException"></exception>
    public string GetConnectionString(IConfiguration configuration)
    => UseSecretsManagerAndServiceDiscovery
    ? string.IsNullOrEmpty(Hostname) || Credentials == null || string.IsNullOrEmpty(Credentials.Username) || string.IsNullOrEmpty(Credentials.Password)
        ? throw new InvalidOperationException("Error building the connection string: RabbitMQ settings are incomplete.")
        : $"amqp://{Credentials.Username}:{Credentials.Password}@{Hostname}"
    : SecretsFiles.GetSecretFromConfig(configuration, InvariantConstants.ConfigurationKeys.SecretsFiles.SecretsFileNames.RabbitMQConnectionString);
}

public record RabbitMQCredentials : IBasicCredentials {
    public string Username { get; set; } = default!;
    public string Password { get; set; } = default!;
}

public record PublisherSettings {
    public string? IntegrationExchange { get; init; }
    public string? DomainExchange { get; init; }
}

public record ConsumerSettings {
    public string? IntegrationQueue { get; init; }
    public string? DomainQueue { get; init; }
}