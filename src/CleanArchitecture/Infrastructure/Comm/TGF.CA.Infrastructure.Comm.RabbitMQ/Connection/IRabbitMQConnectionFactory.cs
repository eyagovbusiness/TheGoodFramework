using RabbitMQ.Client;

namespace TGF.CA.Infrastructure.Comm.RabbitMQ.Connection {
    internal interface IRabbitMQConnectionFactory {
        Task<IConnection> GetConnectionAsync();
    }
}