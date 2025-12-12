using Slascone.Client.Interfaces;

namespace TGF.CA.Infrastructure.Licensing.Slascone.Contracts;

/// <summary>
/// Defines a factory for creating instances of ISlasconeClientV2 asynchronously.
/// </summary>
/// <remarks>Implementations of this interface are responsible for providing properly configured ISlasconeClientV2
/// instances. The returned client may be configured with authentication, connection settings, or other dependencies as
/// required by the implementation.</remarks>
public interface ISlasconeClientFactory {
    /// <summary>
    /// Asynchronously retrieves an instance of the ISlasconeClientV2 interface for interacting with the Slascone API.
    /// </summary>
    /// <returns>A task that represents the asynchronous operation. The task result contains an ISlasconeClientV2 instance for
    /// accessing Slascone API features.</returns>
    Task<ISlasconeClientV2> GetClientAsync();
}
