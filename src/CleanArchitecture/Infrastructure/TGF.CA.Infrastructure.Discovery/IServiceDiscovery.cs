using TGF.CA.Application;

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
}
