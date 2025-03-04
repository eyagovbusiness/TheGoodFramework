using Microsoft.Extensions.Configuration;
using TGF.CA.Domain.ExternalContracts;
using TGF.CA.Infrastructure.Secrets;
using TGF.CA.Infrastructure.Secrets.SecretsFiles;
using TGF.Common.Extensions;

namespace TGF.CA.Infrastructure.Comm.RabbitMQ.Settings;

public class RabbitMQSettings {
    private SecretsSourceTypeEnum _secretsSourceType;
    /// <summary>
    /// Specifies the type of secrets source in the configuration used for retrieving the secrets for rabbitMQ.
    /// </summary>
    public string SecretsSourceType {
        get => _secretsSourceType.GetDescription();
        init => _secretsSourceType = Enum.TryParse(typeof(SecretsSourceTypeEnum), value, false, out var result)
                ? (SecretsSourceTypeEnum)result
                : throw new ArgumentException($"Invalid SecretsSourceType: {value}");
    }
    public string? Hostname { get; private set; }
    public RabbitMQCredentials? Credentials { get; private set; }
    public PublisherSettings? Publisher { get; init; }
    public ConsumerSettings? Consumer { get; init; }


    public void SetCredentials(RabbitMQCredentials credentials)
    => Credentials = credentials;

    public void SetHostName(string hostname)
    => Hostname = hostname;

    /// <summary>
    /// Get the connection string for RabbitMQ, either from the secrets manager or from the secrets file specified in the configuration.
    /// </summary>
    /// <returns>The connection string to the RabbitMQ instance.</returns>
    /// <exception cref="NotSupportedException">Thrown when the secrets source type is missconfigured in appsettings.</exception>
    public string GetConnectionString(IConfiguration configuration)
    => _secretsSourceType switch {

        SecretsSourceTypeEnum.File
        => SecretsFiles.GetSecretFromConfig(configuration, InvariantConstants.ConfigurationKeys.SecretsFiles.SecretsFileNames.RabbitMQConnectionString),

        SecretsSourceTypeEnum.SecretsManager
        => string.IsNullOrEmpty(Hostname) || Credentials == null || string.IsNullOrEmpty(Credentials.Username) || string.IsNullOrEmpty(Credentials.Password)
            ? throw new InvalidOperationException("Error building the connection string: RabbitMQ settings are incomplete.")
            : $"amqp://{Credentials.Username}:{Credentials.Password}@{Hostname}",

        _ => throw new NotSupportedException("[ERROR] Error building the connection string: The provided value in appsettings of SecretsSourceType in RabbitMQ section is not supported for RabbitMQ.")
    };
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