using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Diagnostics.HealthChecks;
using Microsoft.Extensions.Options;
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
    public static void AddRabbitMQ(this IServiceCollection aServiceCollection, string aHealthCheckName)
    {
        aServiceCollection.AddTransient<IRabbitMQSettingsFactory, RabbitMQSettingsFactory>();

        //// Retrieve the connection string using our helper method
        //ServiceProvider lServiceProvider = aServiceCollection.BuildServiceProvider();
        //string lConnectionString = GenerateRabbitMqConnectionString(lServiceProvider);

        //aServiceCollection.AddHealthChecks()
        //    .AddRabbitMQ(lConnectionString, name: aHealthCheckName, failureStatus: HealthStatus.Unhealthy);
    }

    //private static string GenerateRabbitMqConnectionString(IServiceProvider aServiceProvider)
    //{
    //    RabbitMQSettings lSettings = aServiceProvider.GetRequiredService<IOptions<RabbitMQSettings>>().Value;
    //    return $"amqp://{lSettings.Credentials?.Username}:{lSettings.Credentials?.Password}@{lSettings.Hostname}/";
    //}

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