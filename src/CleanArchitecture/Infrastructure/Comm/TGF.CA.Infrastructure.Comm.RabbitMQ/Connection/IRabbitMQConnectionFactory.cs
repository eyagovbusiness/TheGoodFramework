using RabbitMQ.Client;

namespace TGF.CA.Infrastructure.Comm.RabbitMQ.Connection {
    /// <summary>
    /// Factory to provide a connection to RabbitMQ.
    /// </summary>
    internal interface IRabbitMQConnectionFactory {
        /// <summary>
        /// Get a connection to RabbitMQ.
        /// </summary>
        /// <returns><see cref="IConnection"/> for RabbitMQ</returns>
        Task<IConnection> GetConnectionAsync();
    }
}