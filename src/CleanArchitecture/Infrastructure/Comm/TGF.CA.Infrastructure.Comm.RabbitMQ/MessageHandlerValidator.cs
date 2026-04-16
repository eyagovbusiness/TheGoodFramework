using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using TGF.CA.Infrastructure.Comm.Consumer.Handler;

namespace TGF.CA.Infrastructure.Comm.RabbitMQ;

/// <summary>
/// Validates message handler registrations to prevent common dependency injection anti-patterns.
/// Specifically detects and prevents the "captive dependency" problem where transient message handlers
/// capture scoped services (like DbContext/Repositories), causing stale data issues.
/// </summary>
internal static class MessageHandlerValidator
{
    /// <summary>
    /// Validates that a message handler does not inject scoped services directly.
    /// Handlers should inherit from ScopedMessageHandlerBase or manually create scopes.
    /// </summary>
    /// <param name="handlerType">The message handler type to validate</param>
    /// <param name="services">The service collection to inspect for service lifetimes</param>
    /// <exception cref="InvalidOperationException">Thrown when a scoped service is injected directly</exception>
    public static void Validate(Type handlerType, IServiceCollection services)
    {
        // Skip validation if handler uses ScopedMessageHandlerBase (correct pattern)
        if (InheritsFromScopedMessageHandlerBase(handlerType))
            return;

        // Check all constructor parameters
        foreach (var constructor in handlerType.GetConstructors())
        {
            foreach (var parameter in constructor.GetParameters())
            {
                var parameterType = parameter.ParameterType;

                // Skip safe dependencies
                if (IsSafeDependency(parameterType))
                    continue;

                // Check if it's a scoped service
                var lifetime = GetServiceLifetime(parameterType, services);
                if (lifetime == ServiceLifetime.Scoped)
                {
                    ThrowCaptiveDependencyError(handlerType, parameterType);
                }
            }
        }
    }

    /// <summary>
    /// Determines the service lifetime by inspecting actual DI registrations.
    /// Falls back to pattern-based detection if not registered yet.
    /// </summary>
    private static ServiceLifetime? GetServiceLifetime(Type serviceType, IServiceCollection services)
    {
        // Check exact type registration
        var descriptor = services.FirstOrDefault(sd => sd.ServiceType == serviceType);
        if (descriptor != null)
            return descriptor.Lifetime;

        // Check open generic registration (e.g., IRepository<> registered, looking for IRepository<Job>)
        if (serviceType.IsGenericType && !serviceType.IsGenericTypeDefinition)
        {
            var openGeneric = serviceType.GetGenericTypeDefinition();
            descriptor = services.FirstOrDefault(sd => sd.ServiceType == openGeneric);
            if (descriptor != null)
                return descriptor.Lifetime;
        }

        // Fallback: pattern-based detection
        return IsScopedByConvention(serviceType) ? ServiceLifetime.Scoped : null;
    }

    /// <summary>
    /// Checks if a dependency is safe to inject into transient handlers.
    /// Safe dependencies are singletons or types that don't hold state.
    /// </summary>
    private static bool IsSafeDependency(Type type)
    {
        // Framework services (usually singleton)
        if (type == typeof(IServiceProvider) ||
            type == typeof(IConfiguration))
            return true;

        // Loggers (singleton or transient, never scoped)
        if (type == typeof(Microsoft.Extensions.Logging.ILogger) ||
            type == typeof(Microsoft.Extensions.Logging.ILoggerFactory))
            return true;

        if (type.IsGenericType && 
            type.GetGenericTypeDefinition() == typeof(Microsoft.Extensions.Logging.ILogger<>))
            return true;

        return false;
    }

    /// <summary>
    /// Pattern-based detection for scoped services based on naming conventions.
    /// Used when service isn't registered yet at validation time.
    /// </summary>
    private static bool IsScopedByConvention(Type type)
    {
        var ns = type.Namespace ?? string.Empty;

        // Check namespace patterns (architectural layers typically scoped)
        if (ns.Contains(".Repositories") ||
            ns.Contains(".DataAccess") ||
            ns.Contains(".Persistence") ||
            ns.Contains(".UseCases"))
            return true;

        // Check type name patterns
        if (type.IsInterface)
        {
            var name = type.Name;
            if (name.EndsWith("Repository") ||
                name.EndsWith("UseCase") ||
                name.Contains("DbContext"))
                return true;
        }

        // Check base types (e.g., DbContext derivatives)
        var baseType = type.BaseType;
        while (baseType != null && baseType != typeof(object))
        {
            if (baseType.Name.Contains("DbContext"))
                return true;
            baseType = baseType.BaseType;
        }

        return false;
    }

    /// <summary>
    /// Checks if a handler inherits from ScopedMessageHandlerBase.
    /// </summary>
    private static bool InheritsFromScopedMessageHandlerBase(Type handlerType)
    {
        var baseType = handlerType.BaseType;
        while (baseType != null && baseType != typeof(object))
        {
            if (baseType.IsGenericType &&
                baseType.GetGenericTypeDefinition().Name == "ScopedMessageHandlerBase`1")
                return true;

            baseType = baseType.BaseType;
        }
        return false;
    }

    /// <summary>
    /// Throws a detailed exception explaining the captive dependency problem and how to fix it.
    /// </summary>
    private static void ThrowCaptiveDependencyError(Type handlerType, Type scopedServiceType)
    {
        var messageType = GetMessageType(handlerType);
        var messageTypeName = messageType?.Name ?? "TMessage";
        var serviceName = scopedServiceType.Name;
        var parameterName = serviceName.StartsWith("I") && serviceName.Length > 1
            ? char.ToLowerInvariant(serviceName[1]) + serviceName.Substring(2)
            : char.ToLowerInvariant(serviceName[0]) + serviceName.Substring(1);

        var errorMessage = 
            $"Message handler '{handlerType.Name}' cannot inject scoped service '{serviceName}' directly.\n\n" +
            
            "PROBLEM:\n" +
            "The handler is registered as Transient but injects a Scoped service. Message handlers may be\n" +
            "reused by the consumer infrastructure, causing the scoped service to become \"captive\". This leads\n" +
            "to stale data issues - for example, a DbContext from a previous message with cached entities.\n\n" +
            
            "SOLUTION:\n" +
            $"Inherit from ScopedMessageHandlerBase and resolve scoped services from the scopedServices parameter:\n\n" +
            
            $"public class {handlerType.Name}(\n" +
            "    IServiceProvider serviceProvider,\n" +
            $"    ILogger<{handlerType.Name}>? logger = null)\n" +
            $"    : ScopedMessageHandlerBase<{messageTypeName}>(serviceProvider, logger)\n" +
            "{\n" +
            "    protected override async Task HandleScoped(\n" +
            $"        {messageTypeName} message,\n" +
            "        IServiceProvider scopedServices,\n" +
            "        CancellationToken cancellationToken)\n" +
            "    {\n" +
            $"        var {parameterName} = scopedServices.GetRequiredService<{serviceName}>();\n" +
            "        // Handle message with fresh instance\n" +
            "    }\n" +
            "}\n\n" +
            
            "See: https://blog.ploeh.dk/2014/06/02/captive-dependency/";

        throw new InvalidOperationException(errorMessage);
    }

    /// <summary>
    /// Extracts the message type from a message handler's generic interface.
    /// </summary>
    private static Type? GetMessageType(Type handlerType)
    {
        var messageHandlerInterface = handlerType.GetInterfaces()
            .FirstOrDefault(i => i.IsGenericType &&
                               i.GetGenericTypeDefinition().Name.Contains("MessageHandler"));

        return messageHandlerInterface?.GetGenericArguments().FirstOrDefault();
    }
}
