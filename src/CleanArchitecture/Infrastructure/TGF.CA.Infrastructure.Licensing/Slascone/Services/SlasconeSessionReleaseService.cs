using Microsoft.Extensions.Logging;
using TGF.CA.Infrastructure.InvariantConstants;
using TGF.CA.Infrastructure.Licensing.Slascone.Contracts;

namespace TGF.CA.Infrastructure.Licensing.Slascone.Services;

public sealed class SlasconeSessionReleaseService(
    IExternalLicenseService externalLicenseService,
    ILogger<SlasconeSessionReleaseService> logger) : ISlasconeSessionReleaseService {

    public async Task CloseSessionAsync(string clientId, string sessionId, CancellationToken cancellationToken = default) {
        ArgumentException.ThrowIfNullOrWhiteSpace(clientId);
        ArgumentException.ThrowIfNullOrWhiteSpace(sessionId);
        cancellationToken.ThrowIfCancellationRequested();

        try {
            await externalLicenseService.CloseSessionAsync(
                ConfigurationKeys.Licensing.SpectronautLicensing.LicenseFileSecretName,
                clientId,
                sessionId);
            logger.LogInformation("[LICENSE] Session closed successfully {SessionId} for client {ClientId}", sessionId, clientId);
        }
        catch (Exception ex) when (ex is not InvalidOperationException and not OperationCanceledException) {
            logger.LogError(ex, "[LICENSE] Closing SLASCONE session {SessionId} for client {ClientId} failed.", sessionId, clientId);
            throw;
        }
    }
}
