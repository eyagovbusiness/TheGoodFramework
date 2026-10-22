using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using TGF.CA.Application.Contracts.Communication;
using TGF.CA.Infrastructure.Comm.Consumer.Host;
using TGF.CA.Infrastructure.Comm.Consumer.Manager;
using TGF.CA.Infrastructure.Comm.Messages;
using TGF.CA.Infrastructure.Comm.Publisher.Domain;
using TGF.CA.Infrastructure.Comm.Publisher.Integration;
using TGF.Common.Extensions;

namespace TGF.CA.Infrastructure.Comm;

public static class CommunicationDependencyInjection {
    public static void AddConsumer<TMessage>(this IServiceCollection serviceCollection) {
        serviceCollection.AddSingleton<IConsumerManager<TMessage>, ConsumerManager<TMessage>>();
        serviceCollection.AddSingleton<IHostedService, ConsumerHostedService<TMessage>>();
    }

    /// <summary>
    /// Opt-in variant of <see cref="AddConsumer{TMessage}(IServiceCollection)"/>: consumption (including resolving the underlying consumer, connecting and subscribing)
    /// waits for <paramref name="activationGate"/>, which returns <c>true</c> to activate once and <c>false</c>/cancels to never activate.
    /// </summary>
    public static void AddConsumer<TMessage>(this IServiceCollection serviceCollection, Func<IServiceProvider, CancellationToken, Task<bool>> activationGate) {
        ArgumentNullException.ThrowIfNull(activationGate);
        serviceCollection.AddSingleton<IConsumerManager<TMessage>, ConsumerManager<TMessage>>();
        serviceCollection.AddSingleton<IHostedService>(serviceProvider => new GatedConsumerHostedService<TMessage>(
            serviceProvider.GetRequiredService<IConsumerManager<TMessage>>(),
            serviceProvider,
            serviceProvider.GetRequiredService<IRetryUtility>(),
            activationGate,
            serviceProvider.GetRequiredService<ILogger<GatedConsumerHostedService<TMessage>>>()));
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
