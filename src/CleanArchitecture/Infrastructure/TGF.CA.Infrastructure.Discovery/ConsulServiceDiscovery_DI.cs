using Consul;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace TGF.CA.Infrastructure.Discovery
{


    /// <summary>
    /// Provides extension methods to register service discovery and associated health checks in the dependency injection container.
    /// </summary>
    public static class DiscoveryDependencyInjection
    {
        /// <summary>
        /// Adds the discovery service and associated configuration to the dependency injection container.
        /// </summary>
        /// <param name="aServiceList">The service collection.</param>
        /// <param name="aConfiguration">The application configuration.</param>
        /// <returns>The updated service collection.</returns>
        public static IServiceCollection AddDiscoveryService(this IServiceCollection aServiceList, IConfiguration aConfiguration)
        {
            var lConfiguredUriAddress = new Uri(aConfiguration["Discovery:Address"] 
                ?? throw new Exception("Error: while adding DiscoveryService, could not get the address from appsettings!!"));
            return aServiceList.AddSingleton<IConsulClient, ConsulClient>(provider => new ConsulClient(consulConfig =>
            {
                consulConfig.Address = lConfiguredUriAddress;
            }))
            .AddSingleton<IServiceDiscovery, ConsulServiceDiscovery>()
            .AddConsulHealthChecks(lConfiguredUriAddress.Host, lConfiguredUriAddress.Port, "ServiceRegistry");
        }

        /// <summary>
        /// Adds health checks specific to the Consul service.
        /// </summary>
        /// <param name="aServiceCollection">The service collection.</param>
        /// <param name="aHostName">The hostname of the Consul instance.</param>
        /// <param name="aPort">The port on which the Consul instance is running.</param>
        /// <param name="aHealthCheckName">The name of the health check.</param>
        /// <returns>The updated service collection.</returns>
        public static IServiceCollection AddConsulHealthChecks(this IServiceCollection aServiceCollection, string aHostName, int aPort, string aHealthCheckName)
            => aServiceCollection
                .AddHealthChecks()
                .AddConsul(setup =>
                {
                    setup.HostName = aHostName;
                    setup.RequireHttps = false;
                    setup.Port = aPort;
                }, name: aHealthCheckName)
                .Services;

    }

}