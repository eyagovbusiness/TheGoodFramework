using Microsoft.Extensions.DependencyInjection;
using TGF.CA.Infrastructure.Comm.Consumer;
using TGF.CA.Infrastructure.Comm.Messages;
using TGF.CA.Infrastructure.Comm.Publisher;
using TGF.CA.Infrastructure.Comm.RabbitMQ.Connection;
using TGF.CA.Infrastructure.Comm.RabbitMQ.Consumer;
using TGF.CA.Infrastructure.Comm.RabbitMQ.Publisher;
using TGF.CA.Infrastructure.Comm.RabbitMQ.Settings;

namespace TGF.CA.Infrastructure.Comm.RabbitMQ;

public static class RabbitMQ_DI {
    public static void AddRabbitMQInfrastructureServices(this IServiceCollection aServiceCollection, string aHealthCheckName) {
        aServiceCollection
        .AddTransient<IRabbitMQSettingsFactory, RabbitMQSettingsFactory>()
        .AddSingleton<IRabbitMQConnectionFactory, RabbitMQConnectionFactory>()
        .AddSingleton<CustomRabbitMQHealthCheck>();//Add health check as singleton before registering it as healcheck to prevent the healhcheck to open a new connection on each check request

        aServiceCollection
        .AddHealthChecks()
        .AddCheck<CustomRabbitMQHealthCheck>(aHealthCheckName);
    }

    public static void AddRabbitMqConsumer<TMessage>(this IServiceCollection aServiceCollection)
        where TMessage : IMessage {
        aServiceCollection.AddConsumer<TMessage>();
        aServiceCollection.AddSingleton<IMessageConsumer<TMessage>, RabbitMQMessageConsumer<TMessage>>();
    }

    public static void AddRabbitMQPublisher<TMessage>(this IServiceCollection aServiceCollection)
        where TMessage : IMessage {
        aServiceCollection.AddPublisher<TMessage>();
        aServiceCollection.AddSingleton<IExternalMessagePublisher<TMessage>, RabbitMQMessagePublisher<TMessage>>();
    }

}