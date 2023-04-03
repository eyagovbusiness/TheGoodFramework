using TGF.CA.Infrastructure.Communication.Consumer.Host;
using TGF.CA.Infrastructure.Communication.Consumer.Manager;
using TGF.CA.Infrastructure.Communication.Messages;
using TGF.CA.Infrastructure.Communication.Publisher.Domain;
using TGF.CA.Infrastructure.Communication.Publisher.Integration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;

namespace  TGF.CA.Infrastructure.Communication;
//CODE FROM https://github.com/ElectNewt/Distribt
public static class CommunicationDependencyInjection
{
    public static void AddConsumer<TMessage>(this IServiceCollection serviceCollection)
    {
        serviceCollection.AddSingleton<IConsumerManager<TMessage>, ConsumerManager<TMessage>>();
        serviceCollection.AddSingleton<IHostedService, ConsumerHostedService<TMessage>>();
    }

    public static void AddPublisher<TMessage>(this IServiceCollection serviceCollection)
    {
        if (typeof(TMessage) == typeof(IntegrationMessage))
        {
            serviceCollection.AddIntegrationBusPublisher();
        }
        else if (typeof(TMessage) == typeof(DomainMessage))
        {
            serviceCollection.AddDomainBusPublisher();
        }
    }

    private static void AddIntegrationBusPublisher(this IServiceCollection serviceCollection)
    {
        serviceCollection.AddTransient<IIntegrationMessagePublisher, DefaultIntegrationMessagePublisher>();
    }


    private static void AddDomainBusPublisher(this IServiceCollection serviceCollection)
    {
        serviceCollection.AddTransient<IDomainMessagePublisher, DefaultDomainMessagePublisher>();
    }
}