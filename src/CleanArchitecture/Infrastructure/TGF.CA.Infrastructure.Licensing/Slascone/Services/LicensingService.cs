using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Slascone.Client;
using Slascone.Client.Interfaces;
using System.Runtime.CompilerServices;
using TGF.CA.Infrastructure.InvariantConstants;
using TGF.CA.Infrastructure.Licensing.Slascone.Contracts;
using TGF.CA.Infrastructure.Licensing.Slascone.Helpers;
using TGF.CA.Infrastructure.Secrets.SecretsFiles;

namespace TGF.CA.Infrastructure.Licensing.Slascone.Services;

/// <summary>
/// SLASCONE API error codes
/// </summary>
/// <remarks>
/// https://support.slascone.com/hc/en-us/articles/360016160398-ERROR-CODES
/// </remarks>
internal enum SlasconeAPIErrors {
    NONE = 0,
    INVALID_KEY = 1000,
    EXPIRED_KEY = 1001,
    NOT_ACTIVATED = 1002,
    NON_COMPLIANT_VERSION = 1003,
    EXCEEDED_ALLOWED_CONNECTIONS = 1007,
    TOKEN_ALREADY_ASSINGED = 2001,
    UNKNOWN_CLIENT = 2006,
    API_CRASH = 9998,
    UNKNOWN = 9999
}

internal sealed class LicensingService(
    ISlasconeClientFactory slasconeClientFactory,
    ISecretFilesService secretFilesService,
    ISlasconeLicensePrinter licensePrinter,
    IDeviceInfoService deviceInfoService,
    IOptions<SlasconeOptions> slasconeOptions,
    ILogger<LicensingService> logger, 
    IConfiguration configuration
) : ILicensingService {

    public LicenseInfoDto? LicenseInfo { get; private set; }
    public SessionStatusDto? SessionInfo { get; private set; }
    public LicenseComplianceStatus ComplianceStatus => GetLicenseComplianceStatus();
    public LicenseSessionStatus LastOpenSessionAttemptStatus { get; private set; } = LicenseSessionStatus.None;
    public LicenseHeartbeatStatus HeartbeatStatus { get; private set; } = LicenseHeartbeatStatus.None;
    public LicenseActivationStatus ActivationStatus { get; private set; } = LicenseActivationStatus.Unassigned;
    public Lazy<Task<ISlasconeClientV2>> SlasconeClient => new(GetNewSlasconeClient(slasconeClientFactory));
    public IDictionary<Guid, string> LimitationMap { get; private set; } = new Dictionary<Guid, string>();

    private Guid? _tokenId;
    private readonly Guid _sessionId = Guid.NewGuid();
    private readonly string _softwareVersion = Environment.GetEnvironmentVariable(EnvironmentVariableNames.SOFTWARE_VERSION) 
        ?? throw new InvalidOperationException($"[LICENSING][ERROR]: {EnvironmentVariableNames.SOFTWARE_VERSION} environment variable is not set.");

    public async Task ActivateLicenseAsync() {
        try {
            var activateClientDto = new ActivateClientDto {
                Product_id = slasconeOptions.Value.ProductId,
                License_key = await GetLicenseKeyFromSecretFile(),
                Client_id = deviceInfoService.GetUniqueDeviceId(),
                Client_description = "",
                Client_name = configuration[ConfigurationKeys.AppMetadata.ServiceName],
                Software_version = _softwareVersion,
            };
            var slasconeClient = await SlasconeClient.Value;
            var result = await SlasconeErrorHandlingHelper.Execute(slasconeClient.Provisioning.ActivateLicenseAsync, activateClientDto);

            if (null == result.data) {
                ReportError(result);
                if (result.error is not { Id: (int)SlasconeAPIErrors.TOKEN_ALREADY_ASSINGED })
                    ActivationStatus = LicenseActivationStatus.ActivationFailed;
                return;

            }

            var licenseInfoDto = result.data;
            _tokenId = licenseInfoDto.Token_key;
            LimitationMap = licensePrinter.PrintLicenseDetails(licenseInfoDto);
            ActivationStatus = LicenseActivationStatus.Activated;

        }
        catch (Exception ex) {
            ActivationStatus = LicenseActivationStatus.ActivationFailed;
            logger.LogError(ex, "[LICENSE] License activation failed.");
        }
    }

    public async Task AddHeartbeatAsync() {
        var heartbeatDto = new AddHeartbeatDto {
            Product_id = slasconeOptions.Value.ProductId,
            Client_id = deviceInfoService.GetUniqueDeviceId(),
            Software_version = _softwareVersion,
            Operating_system = deviceInfoService.GetOperatingSystem()
        };

        try {
            var slasconeClient = await SlasconeClient.Value;
            var result= await SlasconeErrorHandlingHelper.Execute(slasconeClient.Provisioning.AddHeartbeatAsync, heartbeatDto);

            if (null == result.data) {
                ReportError(result);

                if (result.error is not { Id: (int)SlasconeAPIErrors.UNKNOWN_CLIENT }) {
                    // A common error when the license is not activated yet for the current device.
                    // A typical handling could be to ask the user for a license key and call ActivateLicenseAsync.
                    logger.LogWarning("[LICENSE] The license has to be activated first.");
                }
                HeartbeatStatus = LicenseHeartbeatStatus.Failed;
                return;
            }

            

            var licenseInfoDto = result.data;
            _tokenId = licenseInfoDto.Token_key;
            LicenseInfo = licenseInfoDto;
            HeartbeatStatus = LicenseHeartbeatStatus.Success;
            if(ActivationStatus != LicenseActivationStatus.Activated) // If heartbeat is successful, set activation status to activated. Only activeated devices add heartbeats successfully.
                ActivationStatus = LicenseActivationStatus.Activated;
            LimitationMap = licensePrinter.PrintLicenseDetails(licenseInfoDto);
        }
        catch (Exception ex) {
            HeartbeatStatus = LicenseHeartbeatStatus.Failed;
            logger.LogError(ex, "[LICENSE] Adding heartbeat failed.");
        }
    }

    public async Task UnassignLicenseAsync() {
        if (!_tokenId.HasValue) {
            logger.LogWarning("[LICENSE] You have to add a license heartbeat first to get a token for this operation.");
            return;
        }

        var unassignDto = new UnassignDto {
            Token_key = _tokenId.Value
        };

        try {
            var slasconeClient = await SlasconeClient.Value;
            var result = await SlasconeErrorHandlingHelper.Execute(slasconeClient.Provisioning.UnassignLicenseAsync, unassignDto);

            if (null == result.data) {
                ReportError(result);
                return;
            }
            logger.LogInformation("[LICENSE] License unassigned successfully.");

            // Clear the token id, and limitation map after unassigning
            _tokenId = null;
            LimitationMap = null!;
        }
        catch (Exception ex) {
            logger.LogError(ex, "[LICENSE] Unassigning license failed.");
        }
    }

    public async Task AddAnalyticalHeartbeatAsync(Guid analyticaFieldId, string value) {
        var analyticalHeartbeatDto = new AnalyticalHeartbeatDto {
            Analytical_heartbeat = [],
            Client_id = deviceInfoService.GetUniqueDeviceId()
        };

        var analyticalField = new AnalyticalFieldValueDto {
            Analytical_field_id = analyticaFieldId,
            Value = value
        };
        analyticalHeartbeatDto.Analytical_heartbeat.Add(analyticalField);

        try {
            var slasconeClient = await SlasconeClient.Value;
            var result = await SlasconeErrorHandlingHelper.Execute(slasconeClient.DataGathering.AddAnalyticalHeartbeatAsync, analyticalHeartbeatDto);

            if (null == result.data) {
                ReportError(result);
                return;
            }

            logger.LogInformation("[LICENSE] Analytical heartbeat received: {Data}", result.data);
        }
        catch (Exception ex) {
            logger.LogError(ex, "[LICENSE] Adding analytical heartbeat failed.");
        }
    }

    public async Task AddUsageHeartbeatAsync(IEnumerable<(Guid usageFieldId, double value)> usages) {
        var usageHeartbeat = new FullUsageHeartbeatDto {
            Usage_heartbeat = [],
            Client_id = deviceInfoService.GetUniqueDeviceId()
        };

        foreach (var (usageFieldId, value) in usages) {
            var usageFeatureValue = new UsageHeartbeatValueDto {
                Usage_feature_id = usageFieldId,
                Value = value
            };
            usageHeartbeat.Usage_heartbeat.Add(usageFeatureValue);
        }

        try {
            var slasconeClient = await SlasconeClient.Value;
            var result = await SlasconeErrorHandlingHelper.Execute((uhb) => slasconeClient.DataGathering.AddUsageHeartbeatAsync(uhb, true), usageHeartbeat);

            if (null == result.data) {
                ReportError(result);
                return;
            }
            logger.LogInformation("[LICENSE] Usage heartbeat received: {Data}", result.data);
        }
        catch (Exception ex) {
            logger.LogError(ex, "[LICENSE] Adding usage heartbeat failed.");
        }

    }

    public async Task AddConsumptionHeartbeatAsync(IEnumerable<(Guid, decimal)> consumptions) {
        var consumptionHeartbeat = new FullConsumptionHeartbeatDto {
            Client_id = deviceInfoService.GetUniqueDeviceId(),
            Consumption_heartbeat = []
        };

        foreach (var consumption in consumptions) {
            var consumptionFeatureValue = new ConsumptionHeartbeatValueDto {
                Limitation_id = consumption.Item1,
                Value = consumption.Item2
            };
            consumptionHeartbeat.Consumption_heartbeat.Add(consumptionFeatureValue);
        }

        try {
            var slasconeClient = await SlasconeClient.Value;

            var result = await SlasconeErrorHandlingHelper.Execute(slasconeClient.DataGathering.AddConsumptionHeartbeatAsync, consumptionHeartbeat);

            if (null == result.data) {
                ReportError(result);
                return;
            }

            foreach (var consumptionDto in result.data) {
                var limitation = LimitationMap.TryGetValue(consumptionDto.Limitation_id, out var limitationName)
                    ? $"'{limitationName}' ({consumptionDto.Limitation_id})"
                    : consumptionDto.Limitation_id.ToString();

                if (null != consumptionDto.Transaction_id)
                    logger.LogInformation("[LICENSE] Consumption recorded for {Limitation}: Transaction ID: {Transaction_id}", limitation, consumptionDto.Transaction_id);
                else
                    logger.LogWarning("[LICENSE] Consumption for {Limitation} was not recorded: Limit reached!", limitation);
            }
        }
        catch (Exception ex) {
            logger.LogError(ex, "[LICENSE] Adding consumption heartbeat failed.");
        }
    }

    public async Task OpenSessionAsync() {
        var licensekey = await GetLicenseKeyFromSecretFile();
        if (string.IsNullOrEmpty(licensekey)) {
            LastOpenSessionAttemptStatus = LicenseSessionStatus.OpenFailed;
            logger.LogWarning("[LICENSE] You have to add a license heartbeat first.");
            return;
        }
        try {
            var sessionDto = new SessionRequestDto {
                Client_id = deviceInfoService.GetUniqueDeviceId(),
                License_id = Guid.Parse(licensekey),
                Session_id = _sessionId
            };

            var slasconeClient = await SlasconeClient.Value;
            var result = await SlasconeErrorHandlingHelper.Execute(slasconeClient.Provisioning.OpenSessionAsync, sessionDto);

            if (null == result.data) {
                ReportError(result);

                if (result.error is { Id: (int)SlasconeAPIErrors.EXCEEDED_ALLOWED_CONNECTIONS }) {
                    // This error indicates that the maximum number of allowed parallel sessions has been reached.
                    // Normally you would inform the user about this and prevent the usage of the software.
                    logger.LogWarning("[LICENSE] Maximum of allowed parallel opened sessions exceeded!");
                }
                LastOpenSessionAttemptStatus = LicenseSessionStatus.OpenFailed;
                return;
            }

            var sessionStatus = result.data;
            SessionInfo = sessionStatus;
            logger.LogInformation("[LICENSE] Session opened successfully {SessionId}", _sessionId);
            logger.LogInformation("[LICENSE] Max number of concurrent sessions: {MaxOpenSessionCount}", sessionStatus.Max_open_session_count);
            logger.LogInformation("[LICENSE] Session valid until {SessionValidUntil}", sessionStatus.Session_valid_until);
            LastOpenSessionAttemptStatus = LicenseSessionStatus.Opened;
        }
        catch (Exception ex) {
            LastOpenSessionAttemptStatus = LicenseSessionStatus.OpenFailed;
            logger.LogError(ex, "[LICENSE] Opening session failed.");
        }
    }

    public async Task CloseSessionAsync() {
        var licensekey = await GetLicenseKeyFromSecretFile();
        if (string.IsNullOrEmpty(licensekey)) {
            LastOpenSessionAttemptStatus = LicenseSessionStatus.CloseFailed;
            logger.LogWarning("[LICENSE] You have to add a license heartbeat first.");
            return;
        }

        try {
            var sessionDto = new SessionRequestDto {
                Client_id = deviceInfoService.GetUniqueDeviceId(),
                License_id = Guid.Parse(licensekey),
                Session_id = _sessionId
            };

            var slasconeClient = await SlasconeClient.Value;
            var result = await SlasconeErrorHandlingHelper.Execute(slasconeClient.Provisioning.CloseSessionAsync, sessionDto);

            if (null == result.data) {
                ReportError(result);
                LastOpenSessionAttemptStatus = LicenseSessionStatus.CloseFailed;
                return;
            }

            LastOpenSessionAttemptStatus = LicenseSessionStatus.Closed;
            logger.LogInformation("[LICENSE] Session closed successfully {SessionId}", _sessionId);
        }
        catch (Exception ex) {
            LastOpenSessionAttemptStatus = LicenseSessionStatus.CloseFailed;
            logger.LogError(ex, "[LICENSE] Closing session failed.");
            throw;
        }
    }
    
    public async Task LookupLicensesAsync() {
        var licensekey = await GetLicenseKeyFromSecretFile();
        var getLicenses = new GetLicensesByLicenseKeyDto {
            Product_id = slasconeOptions.Value.ProductId,
            License_key = licensekey
        };

        try {
            var slasconeClient = await SlasconeClient.Value;
            var result = await SlasconeErrorHandlingHelper.Execute(slasconeClient.Provisioning.GetLicensesByLicenseKeyAsync, getLicenses);
                
            if (null == result.data) {
                ReportError(result);
                return;
            }

            var licenseDtos = result.data;
            logger.LogInformation("[LICENSE] Found {LicenseCount} license(s) for key '{LicenseKey}'", licenseDtos.Count, licensekey);

            foreach (var licenseDto in licenseDtos) {
                licensePrinter.PrintLicenseDetails(licenseDto);
            }
        }
        catch (Exception exception) {
            logger.LogError(exception, "[LICENSE] Looking up licenses failed.");
        }
    }

    #region Private Methods

    /// <summary>
    /// Used for Lazy initialization <see cref="SlasconeClient"/>.
    /// </summary>
    private static async Task<ISlasconeClientV2> GetNewSlasconeClient(ISlasconeClientFactory slasconeClientFactory)
        => await slasconeClientFactory.GetClientAsync();

    /// <summary>
    /// Used for Lazy initialization of the _licenseKey.
    /// </summary>
    private async Task<string> GetLicenseKeyFromSecretFile()
        => await secretFilesService.GetSecretFromConfigAsync(ConfigurationKeys.SecretsFiles.SecretsFileNames.LicenseKeySecret);

    /// <summary>
    /// Reports errors from SLASCONE API operations to the the logger.
    /// </summary>
    /// <typeparam name="T">The type of data expected from the API operation.</typeparam>
    /// <param name="result">The result tuple containing data, error type, error object, and error message.</param>
    /// <param name="caller">The name of the calling method (automatically provided by compiler).</param>
    private void ReportError<T>((T data, SlasconeErrorHandlingHelper.ErrorType errorType, ErrorResultObjects error, string message) result, [CallerMemberName] string caller = "") {
        logger.LogError("[LICENSE] Error during {Caller}:", caller);

        if (null != result.error) {
            logger.LogError("[LICENSE] Error code: {ErrorId}", result.error.Id);
            logger.LogError("[LICENSE] Error description: {ErrorMessage}", result.error.Message);
        } else {
            logger.LogError("[LICENSE] Error type: {ErrorType}", result.errorType.ToString());
            logger.LogError("[LICENSE] Error message: {ErrorMessage}", result.message);
        }
    }

    /// <summary>
    /// Determines the current license compliance status based on license validity, session state, and heartbeat status.
    /// </summary>
    /// <remarks>This method evaluates multiple factors to determine compliance, including license validity,
    /// session expiration, session status, and heartbeat status. Use this method to check if the application is
    /// operating under a valid and active license session.</remarks>
    /// <returns>A value indicating whether the license is compliant or non-compliant. Returns <see
    /// cref="LicenseComplianceStatus.Compliant"/> if the license is valid, the session is active, and the heartbeat is
    /// successful; otherwise, returns <see cref="LicenseComplianceStatus.NonCompliant"/>.</returns>
    private LicenseComplianceStatus GetLicenseComplianceStatus()
    => (LicenseInfo?.Is_license_valid ?? false)
    && SessionInfo is not null && SessionInfo.Session_valid_until > DateTimeOffset.Now
    && HeartbeatStatus is LicenseHeartbeatStatus.Success
        ? LicenseComplianceStatus.Compliant
        : LicenseComplianceStatus.NonCompliant;

    #endregion

}
