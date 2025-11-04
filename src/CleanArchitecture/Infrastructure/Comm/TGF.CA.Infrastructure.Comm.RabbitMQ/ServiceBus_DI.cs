using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using TGF.CA.Infrastructure.Comm.Consumer.Handler;
using TGF.CA.Infrastructure.Comm.Messages;

namespace TGF.CA.Infrastructure.Comm.RabbitMQ;

public static class ServiceBus_DI {
    /// <summary>
    /// Adds an integration message publisher. It also ensures the required services for RabbitMQ infrastructure are added.
    /// </summary>
    public static void AddServiceBusIntegrationPublisher(this IServiceCollection aServiceCollection, IConfiguration configuration) {
        aServiceCollection.AddRabbitMQInfrastructureServices(configuration, "IntegrationPublisher");
        aServiceCollection.AddRabbitMQPublisher<IntegrationMessage>();
    }
    /// <summary>
    /// Adds an integration message consumer. It also ensures the required services for RabbitMQ infrastructure are added.
    /// </summary>
    public static void AddServiceBusIntegrationConsumer(this IServiceCollection aServiceCollection, IConfiguration configuration) {
        aServiceCollection.AddRabbitMQInfrastructureServices(configuration, "IntegrationConsumer");
        aServiceCollection.AddRabbitMqConsumer<IntegrationMessage>();
    }
    /// <summary>
    /// Adds a domain message publisher. It also ensures the required services for RabbitMQ infrastructure are added.
    /// </summary>
    public static void AddServiceBusDomainPublisher(this IServiceCollection aServiceCollection, IConfiguration configuration) {
        aServiceCollection.AddRabbitMQInfrastructureServices(configuration, "DomainPublisher");
        aServiceCollection.AddRabbitMQPublisher<DomainMessage>();
    }
    /// <summary>
    /// Adds a domain message comsumer. It also ensures the required services for RabbitMQ infrastructure are added.
    /// </summary>
    public static void AddServiceBusDomainConsumer(this IServiceCollection aServiceCollection, IConfiguration configuration) {
        aServiceCollection.AddRabbitMQInfrastructureServices(configuration, "DomainConsumer");
        aServiceCollection.AddRabbitMqConsumer<DomainMessage>();
    }

    /// <summary>
    /// Adds all the message handlers(classes implementing <see cref="IMessageHandler"/> found in the specified assembly <typeparamref name="T"/>.
    /// </summary>
    /// <typeparam name="T">marker type to determine the assmebly to reference.</typeparam>
    /// <exception cref="InvalidOperationException">Thrown then the method is called but no implementation of <see cref="IMessageHandler"/> was found in the marked assembly.</exception>
    public static void AddMessageHandlersInAssembly<T>(this IServiceCollection aaServiceCollection) {
        // Add IMessageHandler implementations as transient.
        // Find all non-abstract, non-generic classes implementing IMessageHandler in T's assembly
        var lHandlerTypes = typeof(T).Assembly.GetTypes()
            .Where(t => t.IsClass && !t.IsAbstract && !t.IsGenericType && typeof(IMessageHandler).IsAssignableFrom(t));

        // Check if at least one implementation of IMessageHandler is found.
        var lMessageHandlerCount = lHandlerTypes.Count();
        if (lMessageHandlerCount < 1)
            throw new InvalidOperationException("AddHandlersInAssembly<T>() was called but any implementation of IMessageHandler was found during the scan in T's assembly. Please add at lest one or remove the call of this method.");

        foreach (var handlerType in lHandlerTypes) {
            aaServiceCollection.AddTransient(typeof(IMessageHandler), handlerType);
            // Optionally, register for all interfaces the handler implements (except IMessageHandler itself)
            foreach (var iface in handlerType.GetInterfaces().Where(i => i != typeof(IMessageHandler) && typeof(IMessageHandler).IsAssignableFrom(i))) {
                aaServiceCollection.AddTransient(iface, handlerType);
            }
        }

        // Add the IMessageHandlerRegistry which depends on a collection of all IMessageHandler registered above.
        aaServiceCollection.AddSingleton<IMessageHandlerRegistry, MessageHandlerRegistry>();
        // Add IHandleMessage implementation which depends on the IMessageHandlerRegistry registered above.
        aaServiceCollection.AddSingleton<IHandleMessage, HandleMessage>();
    }

}

