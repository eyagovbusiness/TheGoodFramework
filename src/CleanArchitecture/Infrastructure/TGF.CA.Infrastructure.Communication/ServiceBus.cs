﻿//CODE FROM https://github.com/ElectNewt/Distribt
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using TGF.CA.Infrastructure.Communication.Consumer.Handler;
using TGF.CA.Infrastructure.Communication.Messages;
using TGF.CA.Infrastructure.Communication.RabbitMQ;
using TGF.CA.Infrastructure.Discovery;
using TGF.CA.Infrastructure.Secrets.Vault;

namespace TGF.CA.Application.Services
{
    public static class ServiceBus
    {
        public static void AddServiceBusIntegrationPublisher(this IServiceCollection serviceCollection,
            IConfiguration configuration)
        {
            serviceCollection.AddRabbitMQ(GetRabbitMqSecretCredentials, GetRabbitMQHostName,
                configuration, "IntegrationPublisher");
            serviceCollection.AddRabbitMQPublisher<IntegrationMessage>();
        }

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

        /// <summary>
        /// this option is used to show the usage of different engines on Vault
        /// </summary>
        private static async Task<RabbitMQCredentials> GetRabbitMqSecretCredentialsfromRabbitMQEngine(
            IServiceProvider serviceProvider)
        {
            var secretManager = serviceProvider.GetService<ISecretsManager>();
            var credentials = await secretManager!.GetRabbitMQCredentials("distribt-role");
            return new RabbitMQCredentials() { password = credentials.Password, username = credentials.Username };
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

        public static void AddHandlersInAssembly<T>(this IServiceCollection serviceCollection)
        {
            serviceCollection.Scan(scan => scan.FromAssemblyOf<T>()
                .AddClasses(classes => classes.AssignableTo<IMessageHandler>())
                .AsImplementedInterfaces()
                .WithTransientLifetime());

            ServiceProvider sp = serviceCollection.BuildServiceProvider();
            var listHandlers = sp.GetServices<IMessageHandler>();
            serviceCollection.AddConsumerHandlers(listHandlers);
        }

        private static async Task<string> GetRabbitMQHostName(IServiceProvider serviceProvider)
        {
            var serviceDiscovery = serviceProvider.GetService<IServiceDiscovery>();
            return await serviceDiscovery?.GetFullAddress(InfraServicesRegistry.RabbitMQMessageBroker)!;
        }
    }
}
