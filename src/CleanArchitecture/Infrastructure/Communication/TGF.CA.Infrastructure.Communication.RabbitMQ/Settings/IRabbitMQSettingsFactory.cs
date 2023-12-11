using TGF.CA.Infrastructure.Discovery;
using TGF.CA.Application;
namespace TGF.CA.Infrastructure.Communication.RabbitMQ.Settings
{
    /// <summary>
    /// RabbitMQ settings factory where settings are built reading hostname from the <see cref="IServiceDiscovery"/> and credentials from the <see cref="ISecretsManager"/>.
    /// </summary>
    public interface IRabbitMQSettingsFactory
    {
        /// <summary>
        /// Get the <see cref="RabbitMQSettings"/>.
        /// </summary>
        /// <returns>An async task that returns a new instance of <see cref="RabbitMQSettings"/>.</returns>
        Task<RabbitMQSettings> GetRabbitMQSettingsAsync();
    }
}