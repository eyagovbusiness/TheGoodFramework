using RabbitMQ.Client;

namespace TGF.CA.Infrastructure.Comm.RabbitMQ.Connection;

internal class RabbitMQConnectionFactory : IRabbitMQConnectionFactory {
    private readonly Lazy<Task<ConnectionFactory>> _connectionFactory;
    private readonly Lazy<Task<IConnection>> _connection;

    public RabbitMQConnectionFactory(IRabbitMQConnectionStringProvider rabbitMQConnectionStringProvider) {
        _connectionFactory = new(() => GetConnectionFactory(rabbitMQConnectionStringProvider));
        _connection = new(async () => GetSingleConnection(await _connectionFactory.Value));
    }

    public async Task<IConnection> GetConnectionAsync()
    => await _connection.Value;

    #region
    private static async Task<ConnectionFactory> GetConnectionFactory(IRabbitMQConnectionStringProvider rabbitMQConnectionStringProvider) {
        var formattedUri = (await rabbitMQConnectionStringProvider.GetConnectionString()).Replace("https://", string.Empty);//remove protocol of present in hostname: amqps://{username}:{password}@{hostname}:{port}/{vhost}
        return new ConnectionFactory {
            Uri = new Uri(formattedUri)
        };
    }

    private static IConnection GetSingleConnection(ConnectionFactory connectionFactory)
    => connectionFactory.CreateConnection();
    #endregion

}

