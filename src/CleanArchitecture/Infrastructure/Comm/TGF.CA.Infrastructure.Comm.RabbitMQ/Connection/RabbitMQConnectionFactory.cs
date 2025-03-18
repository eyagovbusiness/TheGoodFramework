using Microsoft.Extensions.Configuration;
using RabbitMQ.Client;
using TGF.CA.Infrastructure.Comm.RabbitMQ.Settings;

namespace TGF.CA.Infrastructure.Comm.RabbitMQ.Connection;
internal class RabbitMQConnectionFactory : IRabbitMQConnectionFactory {
    private readonly Lazy<Task<ConnectionFactory>> _connectionFactory;
    private readonly Lazy<Task<IConnection>> _connection;

    public RabbitMQConnectionFactory(IRabbitMQSettingsFactory rabbitMQSettingsFactory, IConfiguration configuration) {
        _connectionFactory = new(() => GetConnectionFactory(rabbitMQSettingsFactory, configuration));
        _connection = new(async () => GetSingleConnection(await _connectionFactory.Value));
    }

    public async Task<IConnection> GetConnectionAsync()
    => await _connection.Value;

    //    using var lConnection = await RetryUtility.ExecuteWithRetryAsync(
    //    rabbitMQConnectionFactory.GetConnectionAsync,
    //    _ => false, // Retry only on exceptions.
    //    aMaxRetries: 10, // Customize max retries as needed.
    //    aDelayMilliseconds: 2000, // Customize delay between retries.
    //    aCancellationToken // Pass the provided CancellationToken.
    //);

    private static async Task<ConnectionFactory> GetConnectionFactory(IRabbitMQSettingsFactory settingsFactory, IConfiguration configuration) {
        var settings = await settingsFactory.GetRabbitMQSettingsAsync();
        var formattedUri = settings.GetConnectionString(configuration).Replace("https://", string.Empty);//remove protocil of present in hostname: amqps://{username}:{password}@{hostname}:{port}/{vhost}
        return new ConnectionFactory {
            Uri = new Uri(formattedUri)
        };
    }

    private static IConnection GetSingleConnection(ConnectionFactory connectionFactory)
    => connectionFactory.CreateConnection();
}

