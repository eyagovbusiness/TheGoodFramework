using HealthChecks.RabbitMQ;
using Microsoft.Extensions.DependencyInjection;
using TGF.CA.Infrastructure.Communication.Consumer;
using TGF.CA.Infrastructure.Communication.Messages;
using TGF.CA.Infrastructure.Communication.Publisher;
using TGF.CA.Infrastructure.Communication.RabbitMQ.Consumer;
using TGF.CA.Infrastructure.Communication.RabbitMQ.Publisher;
using TGF.CA.Infrastructure.Communication.RabbitMQ.Settings;

//code inspired from https://github.com/ElectNewt/Distribt
namespace TGF.CA.Infrastructure.Communication.RabbitMQ;
public static class RabbitMQ_DI
{
    public static void AddRabbitMQInfrastructureServices(this IServiceCollection aServiceCollection, string aHealthCheckName)
    {
        aServiceCollection.AddTransient<IRabbitMQSettingsFactory, RabbitMQSettingsFactory>();

        aServiceCollection.AddHealthChecks()
        .AddCheck<CusomRabbitMQHealthCheck>(aHealthCheckName);
    }

    public static void AddRabbitMqConsumer<TMessage>(this IServiceCollection aServiceCollection)
        where TMessage : IMessage
    {
        aServiceCollection.AddConsumer<TMessage>();
        aServiceCollection.AddSingleton<IMessageConsumer<TMessage>, RabbitMQMessageConsumer<TMessage>>();
    }

    public static void AddRabbitMQPublisher<TMessage>(this IServiceCollection aServiceCollection)
        where TMessage : IMessage
    {
        aServiceCollection.AddPublisher<TMessage>();
        aServiceCollection.AddSingleton<IExternalMessagePublisher<TMessage>, RabbitMQMessagePublisher<TMessage>>();
    }

}