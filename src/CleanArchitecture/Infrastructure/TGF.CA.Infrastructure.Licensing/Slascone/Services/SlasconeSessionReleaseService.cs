using Microsoft.Extensions.Logging;
using Slascone.Client;
using Slascone.Client.Interfaces;
using System.Runtime.CompilerServices;
using TGF.CA.Infrastructure.InvariantConstants;
using TGF.CA.Infrastructure.Licensing.Slascone.Contracts;
using TGF.CA.Infrastructure.Licensing.Slascone.Helpers;
using TGF.CA.Infrastructure.Secrets.SecretsFiles;

namespace TGF.CA.Infrastructure.Licensing.Slascone.Services;

public sealed class SlasconeSessionReleaseService(
    ISlasconeClientFactory slasconeClientFactory,
    ISecretFilesService secretFilesService,
    ILogger<SlasconeSessionReleaseService> logger) : ISlasconeSessionReleaseService {

    private readonly Lazy<Task<ISlasconeClientV2>> _slasconeClient = new(() => GetNewSlasconeClient(slasconeClientFactory));
    private readonly Lazy<Task<Guid>> _spectronautLicenseId = new(() => GetSpectronautLicenseId(secretFilesService, logger));

    public async Task CloseSessionAsync(Guid clientId, string sessionId, CancellationToken cancellationToken = default) {
        ArgumentException.ThrowIfNullOrWhiteSpace(sessionId);
        cancellationToken.ThrowIfCancellationRequested();

        if (!Guid.TryParse(sessionId, out var parsedSessionId)) {
            throw new InvalidOperationException($"[LICENSE] Cannot close SLASCONE session because SessionId '{sessionId}' is not a valid GUID.");
        }

        var sessionDto = new SessionRequestDto {
            Client_id = clientId.ToString(),
            License_id = await _spectronautLicenseId.Value,
            Session_id = parsedSessionId
        };

        try {
            var slasconeClient = await _slasconeClient.Value;
            var result = await SlasconeErrorHandlingHelper.Execute(slasconeClient.Provisioning.CloseSessionAsync, sessionDto);

            if (result.data is null) {
                ReportError(result);
                throw new InvalidOperationException($"[LICENSE] Closing SLASCONE session {sessionId} for client {clientId} failed.");
            }

            logger.LogInformation("[LICENSE] Session closed successfully {SessionId} for client {ClientId}", sessionId, clientId);
        }
        catch (Exception ex) when (ex is not InvalidOperationException and not OperationCanceledException) {
            logger.LogError(ex, "[LICENSE] Closing SLASCONE session {SessionId} for client {ClientId} failed.", sessionId, clientId);
            throw;
        }
    }

    private static async Task<ISlasconeClientV2> GetNewSlasconeClient(ISlasconeClientFactory slasconeClientFactory)
        => await slasconeClientFactory.GetClientAsync();

    private static async Task<Guid> GetSpectronautLicenseId(ISecretFilesService secretFilesService, ILogger<SlasconeSessionReleaseService> logger) {
        var licenseKeyJson = await secretFilesService.GetSecretFromConfigAsync(ConfigurationKeys.SecretsFiles.SecretsFileNames.SpectronautLicenseKeySecret);
        if (string.IsNullOrWhiteSpace(licenseKeyJson)) {
            throw new InvalidOperationException("[LICENSE] Cannot close SLASCONE session because the license key secret is missing.");
        }

        string? licenseKey = null;
        try {
            using var doc = System.Text.Json.JsonDocument.Parse(licenseKeyJson);
            if (doc.RootElement.TryGetProperty("license_key", out var licenseKeyElement)) {
                licenseKey = licenseKeyElement.GetString();
            }
        } catch (System.Text.Json.JsonException ex) {
            logger.LogError(ex, "[LICENSE] Failed to parse license key JSON.");
            throw new InvalidOperationException("[LICENSE] Cannot close SLASCONE session because the license key secret is invalid JSON.");
        }

        if (string.IsNullOrWhiteSpace(licenseKey)) {
            throw new InvalidOperationException("[LICENSE] Cannot close SLASCONE session because the license key is missing in the secret JSON.");
        }

        if (!Guid.TryParse(licenseKey, out var parsedLicenseId)) {
            throw new InvalidOperationException($"[LICENSE] Cannot close SLASCONE session because license_key '{licenseKey}' is not a valid GUID.");
        }

        return parsedLicenseId;
    }

    private void ReportError<T>((T data, SlasconeErrorHandlingHelper.ErrorType errorType, ErrorResultObjects error, string message) result, [CallerMemberName] string caller = "") {
        logger.LogError("[LICENSE] Error during {Caller}:", caller);

        if (result.error is not null) {
            logger.LogError("[LICENSE] Error code: {ErrorId}", result.error.Id);
            logger.LogError("[LICENSE] Error description: {ErrorMessage}", result.error.Message);
        } else {
            logger.LogError("[LICENSE] Error type: {ErrorType}", result.errorType.ToString());
            logger.LogError("[LICENSE] Error message: {ErrorMessage}", result.message);
        }
    }
}
