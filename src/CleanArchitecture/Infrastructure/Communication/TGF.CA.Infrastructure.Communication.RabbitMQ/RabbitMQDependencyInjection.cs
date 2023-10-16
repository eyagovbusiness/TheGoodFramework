using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Diagnostics.HealthChecks;
using Microsoft.Extensions.Options;
using TGF.CA.Infrastructure.Communication.Consumer;
using TGF.CA.Infrastructure.Communication.Consumer.Handler;
using TGF.CA.Infrastructure.Communication.Messages;
using TGF.CA.Infrastructure.Communication.Publisher;
using TGF.CA.Infrastructure.Communication.RabbitMQ.Consumer;
using TGF.CA.Infrastructure.Communication.RabbitMQ.Publisher;

namespace TGF.CA.Infrastructure.Communication.RabbitMQ;
//CODE FROM https://github.com/ElectNewt/Distribt
public static class RabbitMQDependencyInjection
{
    public static void AddRabbitMQ(this IServiceCollection serviceCollection,
        Func<IServiceProvider, Task<RabbitMQCredentials>> rabbitMqCredentialsFactory,
        Func<IServiceProvider, Task<string>> rabbitMqHostName,
        IConfiguration configuration, string name)
    {
        serviceCollection.AddRabbitMQ(configuration);
        serviceCollection.PostConfigure<RabbitMQSettings>(x =>
        {
            ServiceProvider serviceProvider = serviceCollection.BuildServiceProvider();
            x.SetCredentials(rabbitMqCredentialsFactory.Invoke(serviceProvider).Result);
            x.SetHostName(rabbitMqHostName.Invoke(serviceProvider).Result);
        });

        // Retrieve the connection string using our helper method
        ServiceProvider sp = serviceCollection.BuildServiceProvider();
        string connectionString = GenerateRabbitMqConnectionString(sp);

        serviceCollection.AddHealthChecks()
            .AddRabbitMQ(connectionString, name: name, failureStatus: HealthStatus.Unhealthy);
    }

    private static string GenerateRabbitMqConnectionString(IServiceProvider serviceProvider)
    {
        RabbitMQSettings settings = serviceProvider.GetRequiredService<IOptions<RabbitMQSettings>>().Value;
        return $"amqp://{settings.Credentials?.Username}:{settings.Credentials?.Password}@{settings.Hostname}/";
    }

    //obsolete
    //private static IConnection AddRabbitMqHealthCheck(IServiceProvider serviceProvider)
    //{
    //    RabbitMQSettings settings = serviceProvider.GetRequiredService<IOptions<RabbitMQSettings>>().Value;
    //    ConnectionFactory factory = new ConnectionFactory();
    //    factory.UserName = settings.Credentials?.Username;
    //    factory.Password = settings.Credentials?.Password;
    //    factory.VirtualHost = "/";
    //    factory.HostName = settings.Hostname;
    //    factory.Port = AmqpTcpEndpoint.UseDefaultPort;
    //    return factory.CreateConnection();
    //}

    /// <summary>
    /// this method is used when the credentials are inside the configuration. not recommended.
    /// </summary>
    public static void AddRabbitMQ(this IServiceCollection serviceCollection, IConfiguration configuration)
    {
        serviceCollection.Configure<RabbitMQSettings>(configuration.GetSection("Bus:RabbitMQ"));
    }

    public static void AddConsumerHandlers(this IServiceCollection serviceCollection,
        IEnumerable<IMessageHandler> handlers)
    {
        serviceCollection.AddSingleton<IMessageHandlerRegistry>(new MessageHandlerRegistry(handlers));
        serviceCollection.AddSingleton<IHandleMessage, HandleMessage>();
    }

    public static void AddRabbitMqConsumer<TMessage>(this IServiceCollection serviceCollection)
    {
        serviceCollection.AddConsumer<TMessage>();
        serviceCollection.AddSingleton<IMessageConsumer<TMessage>, RabbitMQMessageConsumer<TMessage>>();
    }

    public static void AddRabbitMQPublisher<TMessage>(this IServiceCollection serviceCollection)
        where TMessage : IMessage
    {
        serviceCollection.AddPublisher<TMessage>();
        serviceCollection.AddSingleton<IExternalMessagePublisher<TMessage>, RabbitMQMessagePublisher<TMessage>>();
    }
}