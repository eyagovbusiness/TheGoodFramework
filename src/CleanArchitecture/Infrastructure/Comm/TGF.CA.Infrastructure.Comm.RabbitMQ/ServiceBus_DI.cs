using Microsoft.Extensions.DependencyInjection;
using TGF.CA.Infrastructure.Comm.Consumer.Handler;
using TGF.CA.Infrastructure.Comm.Messages;

namespace TGF.CA.Infrastructure.Comm.RabbitMQ {
    public static class ServiceBus_DI {
        public static void AddServiceBusIntegrationPublisher(this IServiceCollection aServiceCollection) {
            aServiceCollection.AddRabbitMQInfrastructureServices("IntegrationPublisher");
            aServiceCollection.AddRabbitMQPublisher<IntegrationMessage>();
        }

        public static void AddServiceBusIntegrationConsumer(this IServiceCollection aServiceCollection) {
            aServiceCollection.AddRabbitMQInfrastructureServices("IntegrationConsumer");
            aServiceCollection.AddRabbitMqConsumer<IntegrationMessage>();
        }

        public static void AddServiceBusDomainPublisher(this IServiceCollection aServiceCollection) {
            aServiceCollection.AddRabbitMQInfrastructureServices("DomainPublisher");
            aServiceCollection.AddRabbitMQPublisher<DomainMessage>();
        }

        public static void AddServiceBusDomainConsumer(this IServiceCollection aServiceCollection) {
            aServiceCollection.AddRabbitMQInfrastructureServices("DomainConsumer");
            aServiceCollection.AddRabbitMqConsumer<DomainMessage>();
        }

        public static void AddMessageHandlersInAssembly<T>(this IServiceCollection aaServiceCollection) {
            // Check if at least one implementation of IMessageHandler is found.
            var lMessageHandlerCount = typeof(T).Assembly.GetTypes()
                .Where(t => t.IsClass && !t.IsAbstract && typeof(IMessageHandler).IsAssignableFrom(t))
                .Count();
            if (lMessageHandlerCount < 1)
                throw new InvalidOperationException("AddHandlersInAssembly<T>() was called but any implementation of IMessageHandler was found during the scan in T's assembly. Please add at lest one or remove the call of this method.");

            // Add IMessageHandler implementations as transient.
            aaServiceCollection.Scan(scan => scan.FromAssemblyOf<T>()
                .AddClasses(classes => classes.AssignableTo<IMessageHandler>())
                .AsImplementedInterfaces()
                .WithTransientLifetime());

            // Add the IMessageHandlerRegistry which depends on a collection of all IMessageHandler registered above.
            aaServiceCollection.AddSingleton<IMessageHandlerRegistry, MessageHandlerRegistry>();
            // Add IHandleMessage implementation which depends on the IMessageHandlerRegistry registered above.
            aaServiceCollection.AddSingleton<IHandleMessage, HandleMessage>();
        }

    }
}
