using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Slascone.Client;
using Slascone.Client.Interfaces;
using System.Text.Json;
using TGF.CA.Infrastructure.InvariantConstants;
using TGF.CA.Infrastructure.Licensing.Slascone.Contracts;
using TGF.CA.Infrastructure.Licensing.Slascone.Helpers;
using TGF.CA.Infrastructure.Secrets.SecretsFiles;

namespace TGF.CA.Infrastructure.Licensing.Slascone.Services;

internal sealed class ExternalLicenseService(
    ISlasconeClientFactory slasconeClientFactory,
    ISecretFilesService secretFilesService,
    ILogger<ExternalLicenseService> logger,
    IConfiguration configuration
) : IExternalLicenseService {

    private Lazy<Task<ISlasconeClientV2>> SlasconeClient => new(GetNewSlasconeClient(slasconeClientFactory));

    public async Task CloseManagedSessionAsync(string clientId, string sessionId, CancellationToken cancellationToken = default) {
        ArgumentException.ThrowIfNullOrWhiteSpace(sessionId);
        cancellationToken.ThrowIfCancellationRequested();

        try {
            await CloseSessionAsync(
                ConfigurationKeys.Licensing.Slascone.ManagedExternalLicense.LicenseFileSecretName,
                clientId,
                sessionId);
            logger.LogInformation("[LICENSE] Managed external session closed successfully {SessionId} for client {ClientId}", sessionId, clientId);
        }
        catch (Exception ex) when (ex is not InvalidOperationException and not OperationCanceledException) {
            logger.LogError("[LICENSE] Closing managed external session {SessionId} for client {ClientId} failed. Exception type: {ExceptionType}", sessionId, clientId, ex.GetType().Name);
            throw;
        }
    }

    private async Task CloseSessionAsync(string licenseKeyConfigurationKey, string clientId, string sessionId) {
        ArgumentException.ThrowIfNullOrWhiteSpace(licenseKeyConfigurationKey);
        ArgumentException.ThrowIfNullOrWhiteSpace(clientId);
        ArgumentException.ThrowIfNullOrWhiteSpace(sessionId);

        var licenseId = await GetLicenseIdFromSecretFile(licenseKeyConfigurationKey);
        if (!licenseId.HasValue) {
            logger.LogWarning("[LICENSE] External license secret {LicenseKeyConfigurationKey} does not resolve to a license id.", licenseKeyConfigurationKey);
            return;
        }

        try {
            var sessionDto = new SessionRequestDto {
                Client_id = clientId,
                License_id = licenseId.Value,
                Session_id = ResolveSessionId(sessionId)
            };

            var slasconeClient = await SlasconeClient.Value;
            var result = await SlasconeErrorHandlingHelper.Execute(slasconeClient.Provisioning.CloseSessionAsync, sessionDto);

            if (result.data is null) {
                ReportError(result, nameof(CloseSessionAsync));
                return;
            }

            logger.LogInformation("[LICENSE] External session closed successfully {SessionId} for client {ClientId}", sessionId, clientId);
        }
        catch (Exception ex) {
            logger.LogError("[LICENSE] Closing external session failed for client {ClientId} and session {SessionId}. Exception type: {ExceptionType}", clientId, sessionId, ex.GetType().Name);
            throw;
        }
    }

    private static async Task<ISlasconeClientV2> GetNewSlasconeClient(ISlasconeClientFactory slasconeClientFactory)
        => await slasconeClientFactory.GetClientAsync();

    private async Task<Guid?> GetLicenseIdFromSecretFile(string licenseKeyConfigurationKey) {
        var licenseKeySecret = await GetLicenseSecretContentAsync(licenseKeyConfigurationKey);
        if (string.IsNullOrWhiteSpace(licenseKeySecret))
            return null;

        if (Guid.TryParse(licenseKeySecret, out var directLicenseId))
            return directLicenseId;

        try {
            using var doc = JsonDocument.Parse(licenseKeySecret);
            if (!doc.RootElement.TryGetProperty("license_key", out var licenseKeyElement))
                throw new InvalidOperationException($"[LICENSE] License secret '{licenseKeyConfigurationKey}' does not contain 'license_key'.");

            var parsedLicenseKey = licenseKeyElement.GetString();
            if (!Guid.TryParse(parsedLicenseKey, out var jsonLicenseId))
                throw new InvalidOperationException($"[LICENSE] License secret '{licenseKeyConfigurationKey}' contains an invalid license key.");

            return jsonLicenseId;
        }
        catch (JsonException) {
            // The parser's message can echo characters of the secret, so neither it nor the exception is logged or attached.
            logger.LogError("[LICENSE] Failed to parse secret {LicenseKeyConfigurationKey} as JSON.", licenseKeyConfigurationKey);
            throw new InvalidOperationException($"[LICENSE] License secret '{licenseKeyConfigurationKey}' is neither a GUID nor valid JSON.");
        }
    }

    private async Task<string> GetLicenseSecretContentAsync(string licenseKeyConfigurationKey) {
        if (licenseKeyConfigurationKey != ConfigurationKeys.Licensing.Slascone.ManagedExternalLicense.LicenseFileSecretName)
            return await secretFilesService.GetSecretFromConfigAsync(licenseKeyConfigurationKey);

        var secretsPathEnvVar = configuration[ConfigurationKeys.SecretsFiles.SecretsPathEnvVar]
            ?? throw new InvalidOperationException($"[ERROR]: {ConfigurationKeys.SecretsFiles.SecretsPathEnvVar} is not set in configuration.");
        var secretsPath = Environment.GetEnvironmentVariable(secretsPathEnvVar)
            ?? throw new InvalidOperationException($"[ERROR]: Environment variable '{secretsPathEnvVar}' is not set!");
        var configuredPath = configuration[licenseKeyConfigurationKey]
            ?? throw new KeyNotFoundException($"[ERROR]: Secret name key '{licenseKeyConfigurationKey}' not found in configuration.");

        var fullPath = Path.Combine(secretsPath, configuredPath.TrimStart(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar));
        if (Directory.Exists(fullPath))
            fullPath = Path.Combine(fullPath, "license.json");

        if (!File.Exists(fullPath))
            throw new FileNotFoundException($"[ERROR]: Secret file '{fullPath}' not found.");

        return (await File.ReadAllTextAsync(fullPath)).Trim();
    }

    private static Guid ResolveSessionId(string sessionId)
        => Guid.TryParse(sessionId, out var parsedSessionId)
            ? parsedSessionId
            : throw new InvalidOperationException($"[LICENSE] SessionId '{sessionId}' is not a valid GUID.");

    private void ReportError<T>((T data, SlasconeErrorHandlingHelper.ErrorType errorType, ErrorResultObjects error, string message) result, string operationName) {
        logger.LogError("[LICENSE] Error during external operation {OperationName}.", operationName);

        // Provider/SDK error text and exception messages are untrusted and can carry keys or credentials: only the numeric error id and local category are logged.
        if (result.error is not null)
            logger.LogError("[LICENSE] Error code: {ErrorId}", result.error.Id);

        logger.LogError("[LICENSE] Error type: {ErrorType}", result.errorType.ToString());
    }
}
