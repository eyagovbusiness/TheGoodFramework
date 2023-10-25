//CODE FROM https://github.com/ElectNewt/Distribt
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using TGF.CA.Application;
using TGF.CA.Infrastructure.Communication.Consumer.Handler;
using TGF.CA.Infrastructure.Communication.Messages;
using TGF.CA.Infrastructure.Discovery;

namespace TGF.CA.Infrastructure.Communication.RabbitMQ
{
    public static class ServiceBus
    {
        /// <summary>
        /// default option (KeyValue) to get credentials using Vault 
        /// </summary>
        /// <param name="serviceProvider"></param>
        /// <returns></returns>
        private static async Task<RabbitMQCredentials> GetRabbitMqSecretCredentials(IServiceProvider serviceProvider)
        {
            var secretManager = serviceProvider.GetService<ISecretsManager>();
            return await secretManager!.Get<RabbitMQCredentials>("rabbitmq");
        }

        public static void AddServiceBusIntegrationPublisher(this IServiceCollection serviceCollection,
            IConfiguration configuration)
        {
            serviceCollection.AddRabbitMQ(GetRabbitMqSecretCredentials, GetRabbitMQHostName,
                configuration, "IntegrationPublisher");
            serviceCollection.AddRabbitMQPublisher<IntegrationMessage>();
        }

        public static void AddServiceBusIntegrationConsumer(this IServiceCollection serviceCollection,
            IConfiguration configuration)
        {
            serviceCollection.AddRabbitMQ(GetRabbitMqSecretCredentials, GetRabbitMQHostName, configuration,
                "IntegrationConsumer");
            serviceCollection.AddRabbitMqConsumer<IntegrationMessage>();
        }

        public static void AddServiceBusDomainPublisher(this IServiceCollection serviceCollection,
            IConfiguration configuration)
        {
            serviceCollection.AddRabbitMQ(GetRabbitMqSecretCredentials, GetRabbitMQHostName, configuration,
                "DomainPublisher");
            serviceCollection.AddRabbitMQPublisher<DomainMessage>();
        }

        public static void AddServiceBusDomainConsumer(this IServiceCollection serviceCollection,
            IConfiguration configuration)
        {
            serviceCollection.AddRabbitMQ(GetRabbitMqSecretCredentials, GetRabbitMQHostName, configuration,
                "DomainConsumer");
            serviceCollection.AddRabbitMqConsumer<DomainMessage>();
        }

        public static void AddMessageHandlersInAssembly<T>(this IServiceCollection aServiceCollection)
        {
            // Check if at least one implementation of IMessageHandler is found.
            var lMessageHandlerCount = typeof(T).Assembly.GetTypes()
                .Where(t => t.IsClass && !t.IsAbstract && typeof(IMessageHandler).IsAssignableFrom(t))
                .Count();
            if (lMessageHandlerCount < 1)
                throw new InvalidOperationException("AddHandlersInAssembly<T>() was called but any implementation of IMessageHandler was found during the scan in T's assembly. Please add at lest one or remove the call of this method.");

            // Add IMessageHandler implementations as transient.
            aServiceCollection.Scan(scan => scan.FromAssemblyOf<T>()
                .AddClasses(classes => classes.AssignableTo<IMessageHandler>())
                .AsImplementedInterfaces()
                .WithTransientLifetime());

            // Add the IMessageHandlerRegistry which depends on a collection of all IMessageHandler registered above.
            aServiceCollection.AddSingleton<IMessageHandlerRegistry, MessageHandlerRegistry>();
            // Add IHandleMessage implementation which depends on the IMessageHandlerRegistry registered above.
            aServiceCollection.AddSingleton<IHandleMessage, HandleMessage>();
        }

        private static async Task<string> GetRabbitMQHostName(IServiceProvider serviceProvider)
        {
            var serviceDiscovery = serviceProvider.GetService<IServiceDiscovery>();
            return await serviceDiscovery?.GetFullAddress(InfraServicesRegistry.RabbitMQMessageBroker)!;
        }
    }
}
