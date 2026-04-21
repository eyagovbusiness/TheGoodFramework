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
    /// Closes a managed external session using the configured external license secret.
    /// </summary>
    /// <param name="clientId">The external client identifier to use.</param>
    /// <param name="sessionId">The external session identifier to close.</param>
    /// <param name="cancellationToken">Cancellation token for the operation.</param>
    /// <returns>A task representing the asynchronous operation.</returns>
    Task CloseManagedSessionAsync(string clientId, string sessionId, CancellationToken cancellationToken = default);

}
