using Microsoft.Extensions.Configuration;
using TGF.CA.Infrastructure.Secrets;
using TGF.Common.Extensions;

namespace TGF.CA.Infrastructure.Comm.RabbitMQ.Settings;

/// <summary>
/// Represents the settings for the RabbitMQ bus. 
/// </summary>
internal class RabbitMQSettings {
    /// <summary>
    /// Represents the Bus settings section for RabbitMQ.
    /// </summary>
    public static RabbitMQSettings GetRabbitMQBusSettings(IConfiguration aConfiguration) {
        var lRabbitMQSettings = new RabbitMQSettings();
        aConfiguration.GetSection("Bus:RabbitMQ").Bind(lRabbitMQSettings);
        return lRabbitMQSettings;
    }

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
    /// <summary>
    /// Represents the settings for the publisher.
    /// </summary>
    public PublisherSettings? Publisher { get; init; }
    /// <summary>
    /// Represents the settings for the consumer.
    /// </summary>
    public ConsumerSettings? Consumer { get; init; }

}
/// <summary>
/// Represents the settings for the publisher.
/// </summary>
internal record PublisherSettings {
    public string? IntegrationExchange { get; init; }
    public string? DomainExchange { get; init; }
}

/// <summary>
/// Represents the settings for the consumer.
/// </summary>
internal record ConsumerSettings {
    public string? IntegrationQueue { get; init; }
    public string? DomainQueue { get; init; }
    public int MaxHandlerRetries { get; init; } = 3;
}
