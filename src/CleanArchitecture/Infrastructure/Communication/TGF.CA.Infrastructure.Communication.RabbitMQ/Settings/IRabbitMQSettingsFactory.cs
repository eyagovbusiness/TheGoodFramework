
namespace TGF.CA.Infrastructure.Communication.RabbitMQ.Settings
{
    public interface IRabbitMQSettingsFactory
    {
        Task<RabbitMQSettings> GetRabbitMQSettingsAsync();
    }
}