using TGF.CA.Domain.External;

namespace TGF.CA.Infrastructure.Communication.RabbitMQ;

public class RabbitMQSettings
{
    public string? Hostname { get; private set; }
    public RabbitMQCredentials? Credentials { get; private set; }
    public PublisherSettings? Publisher { get; init; }
    public ConsumerSettings? Consumer { get; init; }

    public void SetCredentials(RabbitMQCredentials credentials)
    {
        Credentials = credentials;
    }

    public void SetHostName(string hostname)
    {
        Hostname = hostname;
    }

    public string GetConnectionString()
    {
        if (string.IsNullOrEmpty(Hostname) || Credentials == null || string.IsNullOrEmpty(Credentials.Username) || string.IsNullOrEmpty(Credentials.Password))
        {
            throw new InvalidOperationException("Error building the connection string: RabbitMQ settings are incomplete.");
        }

        return $"amqp://{Credentials.Username}:{Credentials.Password}@{Hostname}";
    }
}

public record RabbitMQCredentials : IBasicCredentials
{
    public string Username { get; set; } = default!;
    public string Password { get; set; } = default!;
}

public record PublisherSettings
{
    public string? IntegrationExchange { get; init; }
    public string? DomainExchange { get; init; }
}

public record ConsumerSettings
{
    public string? IntegrationQueue { get; init; }
    public string? DomainQueue { get; init; }
}