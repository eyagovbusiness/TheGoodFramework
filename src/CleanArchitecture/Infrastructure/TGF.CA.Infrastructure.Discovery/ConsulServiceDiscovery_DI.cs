using Consul;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using TGF.Common.Extensions;

namespace TGF.CA.Infrastructure.Discovery {
    /// <summary>
    /// Provides extension methods to register service discovery and associated health checks in the dependency injection container.
    /// </summary>
    public static class DiscoveryDependencyInjection {
        /// <summary>
        /// Adds the discovery service and associated configuration to the dependency injection container.
        /// </summary>
        /// <param name="serviceList">The service collection.</param>
        /// <param name="configuration">The application configuration.</param>
        /// <param name="logger">The logger instance.</param>
        /// <returns>The updated service collection.</returns>
        public static IServiceCollection AddDiscoveryService(this IServiceCollection serviceList, IConfiguration configuration, ILogger logger) {
            _ = logger ??
                throw new ArgumentNullException(nameof(logger), "AddDiscoveryService requires a builder logger!");

            var discoveryAddress = configuration["Discovery:Address"];
            if (discoveryAddress.IsNullOrWhiteSpace()) {
                logger.LogInformation("Discovery cofiguration is missing in appsettings, skipping discovery service registration...");
                return serviceList;
            }
            var configuredUriAddress = new Uri(discoveryAddress!);
            return serviceList.AddSingleton<IConsulClient, ConsulClient>(provider => new ConsulClient(consulConfig => {
                consulConfig.Address = configuredUriAddress;
                logger.LogInformation("Consul discovery service successfully configured with address: {Address}", consulConfig.Address);
            }))
            .AddSingleton<IServiceDiscovery, ConsulServiceDiscovery>()
            .AddConsulHealthChecks(configuredUriAddress.Host, configuredUriAddress.Port, "ServiceRegistry");
        }

        /// <summary>
        /// Adds health checks specific to the Consul service.
        /// </summary>
        /// <param name="serviceCollection">The service collection.</param>
        /// <param name="hostName">The hostname of the Consul instance.</param>
        /// <param name="port">The port on which the Consul instance is running.</param>
        /// <param name="healthCheckName">The name of the health check.</param>
        /// <returns>The updated service collection.</returns>
        public static IServiceCollection AddConsulHealthChecks(this IServiceCollection serviceCollection, string hostName, int port, string healthCheckName)
        => serviceCollection
        .AddHealthChecks()
        .AddConsul(setup => {
            setup.HostName = hostName;
            setup.RequireHttps = false;
            setup.Port = port;
        }, name: healthCheckName)
        .Services;
    }

}