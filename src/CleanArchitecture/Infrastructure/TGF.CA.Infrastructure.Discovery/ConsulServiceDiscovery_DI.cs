using Consul;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace TGF.CA.Infrastructure.Discovery
{
    /// <summary>
    /// Provides methods to discover services and retrieve their associated data.
    /// </summary>
    public interface IServiceDiscovery
    {
        /// <summary>
        /// Gets the full address for the provided service key.
        /// </summary>
        /// <param name="aServiceKey">The service key.</param>
        /// <param name="aCancellationToken">A token to monitor for cancellation requests.</param>
        /// <returns>The full address of the service associated with the provided key.</returns>
        Task<string> GetFullAddress(string aServiceKey, CancellationToken aCancellationToken = default);

        /// <summary>
        /// Retrieves the discovery data for the provided service key.
        /// </summary>
        /// <param name="aServiceKey">The service key.</param>
        /// <param name="aCancellationToken">A token to monitor for cancellation requests.</param>
        /// <returns>An instance of <see cref="DiscoveryData"/> containing the service's discovery data.</returns>
        Task<DiscoveryData> GetDiscoveryData(string aServiceKey, CancellationToken aCancellationToken = default);
    }

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
            return aServiceList.AddSingleton<IConsulClient, ConsulClient>(provider => new ConsulClient(consulConfig =>
            {
                var lAddress = aConfiguration["Discovery:Address"] ?? throw new Exception("Error: while adding DiscoveryService, could not get the address from appsettings!!");
                consulConfig.Address = new Uri(lAddress);
            }))
            .AddSingleton<IServiceDiscovery, ConsulServiceDiscovery>()
            .AddConsulHealthChecks("consul", 8500, "ServiceRegistry");
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