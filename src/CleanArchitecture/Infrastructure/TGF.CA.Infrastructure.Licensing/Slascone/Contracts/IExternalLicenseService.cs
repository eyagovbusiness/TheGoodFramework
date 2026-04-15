using Slascone.Client;
using Slascone.Client.Interfaces;

namespace TGF.CA.Infrastructure.Licensing.Slascone.Contracts;

/// <summary>
/// Manages SLASCONE operations for licenses that are external to the running service.
/// </summary>
/// <remarks>
/// This contract is intentionally separate from <see cref="ILicensingService"/> so that opening or closing sessions
/// for 3rd-party products cannot overwrite the in-memory compliance state of the current service.
/// </remarks>
public interface IExternalLicenseService {
    /// <summary>
    /// Gets the SLASCONE client for version 2 of the SLASCONE API.
    /// </summary>
    Lazy<Task<ISlasconeClientV2>> SlasconeClient { get; }

    /// <summary>
    /// Opens a new session with the SLASCONE service using the specified external license secret and identifiers.
    /// </summary>
    /// <param name="licenseKeyConfigurationKey">The configuration key that points to the external license secret file.</param>
    /// <param name="clientId">The client identifier to use.</param>
    /// <param name="sessionId">The session identifier to use.</param>
    /// <returns>A task representing the asynchronous operation.</returns>
    Task<SessionStatusDto?> OpenSessionAsync(string licenseKeyConfigurationKey, string clientId, string sessionId);

    /// <summary>
    /// Closes a managed external session using the configured external license secret.
    /// </summary>
    /// <param name="clientId">The external client identifier to use.</param>
    /// <param name="sessionId">The external session identifier to close.</param>
    /// <param name="cancellationToken">Cancellation token for the operation.</param>
    /// <returns>A task representing the asynchronous operation.</returns>
    Task CloseManagedSessionAsync(Guid clientId, string sessionId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Closes a session with the SLASCONE service using the specified external license secret and identifiers.
    /// </summary>
    /// <param name="licenseKeyConfigurationKey">The configuration key that points to the external license secret file.</param>
    /// <param name="clientId">The client identifier to use.</param>
    /// <param name="sessionId">The session identifier to use.</param>
    /// <returns>A task representing the asynchronous operation.</returns>
    Task CloseSessionAsync(string licenseKeyConfigurationKey, string clientId, string sessionId);
}
