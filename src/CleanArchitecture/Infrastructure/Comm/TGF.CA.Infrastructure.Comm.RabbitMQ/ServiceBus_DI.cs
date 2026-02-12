using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using TGF.CA.Infrastructure.Comm.Consumer.Handler;
using TGF.CA.Infrastructure.Comm.Messages;
using TGF.CA.Infrastructure.InvariantConstants;

namespace TGF.CA.Infrastructure.Comm.RabbitMQ;

public static class ServiceBus_DI {
    /// <summary>
    /// Adds an integration message publisher. It also ensures the required services for RabbitMQ infrastructure are added.
    /// </summary>
    public static void AddServiceBusIntegrationPublisher(this IServiceCollection aServiceCollection, IConfiguration configuration) {
        aServiceCollection.AddRabbitMQInfrastructureServices(configuration, InfrastrcutureConstants.HealthCheckNames.IntegrationPublisher);
        aServiceCollection.AddRabbitMQPublisher<IntegrationMessage>();
    }
    
    /// <summary>
    /// Adds an integration message consumer. It also ensures the required services for RabbitMQ infrastructure are added.
    /// </summary>
    public static void AddServiceBusIntegrationConsumer(this IServiceCollection aServiceCollection, IConfiguration configuration) {
        aServiceCollection.AddRabbitMQInfrastructureServices(configuration, InfrastrcutureConstants.HealthCheckNames.IntegrationConsumer);
        aServiceCollection.AddRabbitMqConsumer<IntegrationMessage>();
    }
    
    /// <summary>
    /// Adds a domain message publisher. It also ensures the required services for RabbitMQ infrastructure are added.
    /// </summary>
    public static void AddServiceBusDomainPublisher(this IServiceCollection aServiceCollection, IConfiguration configuration) {
        aServiceCollection.AddRabbitMQInfrastructureServices(configuration, InfrastrcutureConstants.HealthCheckNames.DomainPublisher);
        aServiceCollection.AddRabbitMQPublisher<DomainMessage>();
    }
    
    /// <summary>
    /// Adds a domain message consumer. It also ensures the required services for RabbitMQ infrastructure are added.
    /// </summary>
    public static void AddServiceBusDomainConsumer(this IServiceCollection aServiceCollection, IConfiguration configuration) {
        aServiceCollection.AddRabbitMQInfrastructureServices(configuration, InfrastrcutureConstants.HealthCheckNames.DomainConsumer);
        aServiceCollection.AddRabbitMqConsumer<DomainMessage>();
    }

    /// <summary>
    /// Scans and registers all message handlers from the assembly containing type <typeparamref name="T"/>.
    /// Validates handlers to prevent captive dependency anti-patterns.
    /// </summary>
    /// <typeparam name="T">Marker type to identify the assembly to scan</typeparam>
    /// <exception cref="InvalidOperationException">Thrown when no handlers found or validation fails</exception>
    public static void AddMessageHandlersInAssembly<T>(this IServiceCollection services) {
        // Find all message handler types in the assembly
        var handlerTypes = typeof(T).Assembly.GetTypes()
            .Where(t => t.IsClass && 
                       !t.IsAbstract && 
                       !t.IsGenericType && 
                       typeof(IMessageHandler).IsAssignableFrom(t))
            .ToList();

        if (handlerTypes.Count == 0)
            throw new InvalidOperationException(
                $"No message handlers found in assembly '{typeof(T).Assembly.GetName().Name}'. " +
                $"Ensure at least one class implements IMessageHandler.");

        // Register and validate each handler
        foreach (var handlerType in handlerTypes) {
            // Validate before registration to catch issues at startup
            MessageHandlerValidator.Validate(handlerType, services);
            
            // Register as transient
            services.AddTransient(typeof(IMessageHandler), handlerType);
            
            // Register for all implemented interfaces (except base IMessageHandler)
            foreach (var iface in handlerType.GetInterfaces()
                .Where(i => i != typeof(IMessageHandler) && 
                          typeof(IMessageHandler).IsAssignableFrom(i))) {
                services.AddTransient(iface, handlerType);
            }
        }

        // Register handler infrastructure
        services.AddSingleton<IMessageHandlerRegistry, MessageHandlerRegistry>();
        services.AddSingleton<IHandleMessage, HandleMessage>();
    }
}

