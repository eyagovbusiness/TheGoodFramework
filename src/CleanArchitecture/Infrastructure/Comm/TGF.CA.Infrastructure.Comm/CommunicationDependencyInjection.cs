using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using TGF.CA.Application.Contracts.Communication;
using TGF.CA.Infrastructure.Comm.Consumer.Host;
using TGF.CA.Infrastructure.Comm.Consumer.Manager;
using TGF.CA.Infrastructure.Comm.Messages;
using TGF.CA.Infrastructure.Comm.Publisher.Domain;
using TGF.CA.Infrastructure.Comm.Publisher.Integration;

namespace TGF.CA.Infrastructure.Comm;

public static class CommunicationDependencyInjection {
    public static void AddConsumer<TMessage>(this IServiceCollection serviceCollection) {
        serviceCollection.AddSingleton<IConsumerManager<TMessage>, ConsumerManager<TMessage>>();
        serviceCollection.AddSingleton<IHostedService, ConsumerHostedService<TMessage>>();
    }

    public static void AddPublisher<TMessage>(this IServiceCollection serviceCollection) {
        if (typeof(TMessage) == typeof(IntegrationMessage)) {
            serviceCollection.AddIntegrationBusPublisher();
        } else if (typeof(TMessage) == typeof(DomainMessage)) {
            serviceCollection.AddDomainBusPublisher();
        }
    }

    private static void AddIntegrationBusPublisher(this IServiceCollection serviceCollection) {
        serviceCollection.AddTransient<IIntegrationMessagePublisher, DefaultIntegrationMessagePublisher>();
    }


    private static void AddDomainBusPublisher(this IServiceCollection serviceCollection) {
        serviceCollection.AddTransient<IDomainMessagePublisher, DefaultDomainMessagePublisher>();
    }
}