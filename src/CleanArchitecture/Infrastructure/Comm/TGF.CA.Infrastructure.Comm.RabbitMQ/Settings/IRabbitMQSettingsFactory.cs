using TGF.CA.Application;
using TGF.CA.Infrastructure.Comm.RabbitMQ;
using TGF.CA.Infrastructure.Discovery;
namespace TGF.CA.Infrastructure.Comm.RabbitMQ.Settings {
    /// <summary>
    /// RabbitMQ settings factory where settings are built reading hostname from the <see cref="IServiceDiscovery"/> and credentials from the <see cref="ISecretsManager"/>.
    /// </summary>
    public interface IRabbitMQSettingsFactory {
        /// <summary>
        /// Get the <see cref="RabbitMQSettings"/>.
        /// </summary>
        /// <returns>An async task that returns a new instance of <see cref="RabbitMQSettings"/>.</returns>
        Task<RabbitMQSettings> GetRabbitMQSettingsAsync();
    }
}