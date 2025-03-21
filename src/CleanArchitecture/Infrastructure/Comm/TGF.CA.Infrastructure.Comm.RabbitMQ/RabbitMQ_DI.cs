using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using TGF.CA.Infrastructure.Comm.Consumer;
using TGF.CA.Infrastructure.Comm.Messages;
using TGF.CA.Infrastructure.Comm.Publisher;
using TGF.CA.Infrastructure.Comm.RabbitMQ.Connection;
using TGF.CA.Infrastructure.Comm.RabbitMQ.Consumer;
using TGF.CA.Infrastructure.Comm.RabbitMQ.Publisher;
using TGF.CA.Infrastructure.Comm.RabbitMQ.Settings;
using TGF.CA.Infrastructure.Secrets;

namespace TGF.CA.Infrastructure.Comm.RabbitMQ;

public static class RabbitMQ_DI {
    public static void AddRabbitMQInfrastructureServices(this IServiceCollection serviceCollection, IConfiguration configuration, string healthCheckName) {

        serviceCollection
        .AddRabbitMQSettings(configuration)
        .AddConnectionSecretsProvider(configuration)
        .AddSingleton<IRabbitMQConnectionFactory, RabbitMQConnectionFactory>()
        .AddSingleton<CustomRabbitMQHealthCheck>();//Add health check as singleton before registering it as healthcheck to prevent the healthcheck to open a new connection on each check request

        serviceCollection
        .AddHealthChecks()
        .AddCheck<CustomRabbitMQHealthCheck>(healthCheckName);
    }

    /// <summary>
    /// Add the connection secrets provider based on the configuration provided in the appsettings.
    /// </summary>
    internal static IServiceCollection AddConnectionSecretsProvider(this IServiceCollection serviceCollection, IConfiguration configuration) {
        var rabbitMQSettings = RabbitMQSettings.GetRabbitMQBusSettings(configuration);
        Enum.TryParse(typeof(SecretsSourceTypeEnum), rabbitMQSettings.SecretsSourceType, false, out var secretsSourceType);
        return secretsSourceType switch {
            SecretsSourceTypeEnum.File => serviceCollection.AddSingleton<IRabbitMQConnectionStringProvider, RabbitMQFileConnectionStringProvider>(),
            SecretsSourceTypeEnum.SecretsManager => serviceCollection.AddSingleton<IRabbitMQConnectionStringProvider, RabbitMQFileConnectionStringProvider>(),
            _ => throw new NotSupportedException("[ERROR] The provided value in appsettings of SecretsSourceType in RabbitMQ section is not a supported secrets provider")
        };
    }

    internal static IServiceCollection AddRabbitMQSettings(this IServiceCollection serviceCollection, IConfiguration configuration)
    => serviceCollection.AddSingleton(RabbitMQSettings.GetRabbitMQBusSettings(configuration));


    internal static void AddRabbitMqConsumer<TMessage>(this IServiceCollection aServiceCollection)
        where TMessage : IMessage {
        aServiceCollection.AddConsumer<TMessage>();
        aServiceCollection.AddSingleton<IMessageConsumer<TMessage>, RabbitMQMessageConsumer<TMessage>>();
    }

    internal static void AddRabbitMQPublisher<TMessage>(this IServiceCollection aServiceCollection)
        where TMessage : IMessage {
        aServiceCollection.AddPublisher<TMessage>();
        aServiceCollection.AddSingleton<IExternalMessagePublisher<TMessage>, RabbitMQMessagePublisher<TMessage>>();
    }

}