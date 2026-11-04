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

internal sealed class LicensingService(
    ISlasconeClientFactory slasconeClientFactory,
    ISecretFilesService secretFilesService,
    ISlasconeLicensePrinter licensePrinter,
    IDeviceInfoService deviceInfoService,
    IOptions<SlasconeOptions> slasconeOptions,
    ILogger<LicensingService> logger,
    IConfiguration configuration
) : ILicensingService {

    private sealed record LicenseState(
        LicenseInfoDto? License = null,
        SessionStatusDto? Session = null,
        LicenseSessionStatus SessionStatus = LicenseSessionStatus.None,
        LicenseHeartbeatStatus Heartbeat = LicenseHeartbeatStatus.None,
        LicenseActivationStatus Activation = LicenseActivationStatus.Unassigned);

    private LicenseState _state = new();
    private readonly SemaphoreSlim _operations = new(1, 1);
    public LicenseInfoDto? LicenseInfo => Volatile.Read(ref _state).License;
    public SessionStatusDto? SessionInfo => Volatile.Read(ref _state).Session;
    public LicensingSnapshot Snapshot {
        get {
            var state = Volatile.Read(ref _state);
            return new(state.License, GetLicenseComplianceStatus(state));
        }
    }
    public LicenseComplianceStatus ComplianceStatus => Snapshot.ComplianceStatus;
    public LicenseSessionStatus LastOpenSessionAttemptStatus {
        get => Volatile.Read(ref _state).SessionStatus;
        private set => Publish(_state with { SessionStatus = value });
    }
    public LicenseHeartbeatStatus HeartbeatStatus {
        get => Volatile.Read(ref _state).Heartbeat;
        private set => Publish(_state with { Heartbeat = value });
    }
    public LicenseActivationStatus ActivationStatus {
        get => Volatile.Read(ref _state).Activation;
        private set => Publish(_state with { Activation = value });
    }
    public Lazy<Task<ISlasconeClientV2>> SlasconeClient { get; } = new(GetNewSlasconeClient(slasconeClientFactory));
    public string ClientId => deviceInfoService.GetUniqueDeviceId();
    public LicensingOperationError? LastError { get; private set; }
    public IDictionary<Guid, string> LimitationMap { get; private set; } = new Dictionary<Guid, string>();

    private Guid? _tokenId;
    private readonly Guid _sessionId = Guid.NewGuid();
    private readonly string _softwareVersion = Environment.GetEnvironmentVariable(EnvironmentVariableNames.SOFTWARE_VERSION)
        ?? throw new InvalidOperationException($"[LICENSING][ERROR]: {EnvironmentVariableNames.SOFTWARE_VERSION} environment variable is not set.");

    public Task ActivateLicenseAsync() => SerializeAsync(ActivateLicenseCoreAsync);
    public Task AddHeartbeatAsync() => SerializeAsync(AddHeartbeatCoreAsync);
    public Task OpenSessionAsync() => SerializeAsync(OpenSessionCoreAsync);
    public async Task CloseSessionAsync(CancellationToken cancellationToken = default) {
        var closing = SerializeAsync(() => CloseSessionCoreAsync(cancellationToken), cancellationToken);
        try {
            await closing.WaitAsync(cancellationToken);
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested) {
            // The SDK cannot cancel an in-flight close. Keep the semaphore until its outcome is reconciled.
            _ = ObserveCloseCompletionAsync(closing);
            throw;
        }
    }

    private async Task ObserveCloseCompletionAsync(Task closing) {
        try {
            await closing;
        }
        catch (OperationCanceledException) {
            logger.LogWarning("[LICENSE] Session close cleanup was cancelled.");
        }
        catch (Exception ex) {
            logger.LogError("[LICENSE] Session close cleanup failed after caller cancellation. Exception type: {ExceptionType}", ex.GetType().Name);
        }
    }
    public Task UnassignLicenseAsync() => SerializeAsync(UnassignLicenseCoreAsync);

    private async Task SerializeAsync(Func<Task> operation, CancellationToken cancellationToken = default) {
        await _operations.WaitAsync(cancellationToken);
        try {
            cancellationToken.ThrowIfCancellationRequested();
            await operation();
        }
        finally {
            _operations.Release();
        }
    }

    private void Publish(LicenseState state) => Volatile.Write(ref _state, state);

    private async Task ActivateLicenseCoreAsync() {
        Publish(_state with { Session = null, Heartbeat = LicenseHeartbeatStatus.None });
        try {
            var activateClientDto = new ActivateClientDto {
                Product_id = slasconeOptions.Value.ProductId,
                License_key = await GetLicenseKeyFromSecretFile(),
                Client_id = ClientId,
                Client_description = "",
                Client_name = configuration[ConfigurationKeys.AppMetadata.ServiceName],
                Software_version = _softwareVersion,
            };
            var slasconeClient = await SlasconeClient.Value;
            var result = await SlasconeErrorHandlingHelper.Execute(slasconeClient.Provisioning.ActivateLicenseAsync, activateClientDto);

            if (null == result.data) {
                ReportError(result);
                if (result.error is not { Id: (int)SlasconeApiErrorCode.TOKEN_ALREADY_ASSINGED })
                    ActivationStatus = LicenseActivationStatus.ActivationFailed;
                return;

            }

            var licenseInfoDto = result.data;
            if (licenseInfoDto.License_key != activateClientDto.License_key
                || licenseInfoDto.License_key != await GetLicenseKeyFromSecretFile()) {
                ClearLicenseState();
                ActivationStatus = LicenseActivationStatus.ActivationFailed;
                logger.LogError("[LICENSE] Activation returned a license different from the configured license.");
                return;
            }
            _tokenId = licenseInfoDto.Token_key;
            LimitationMap = licensePrinter.PrintLicenseDetails(licenseInfoDto);
            Publish(new(License: licenseInfoDto, Activation: LicenseActivationStatus.Activated));
            ClearLastError();

        }
        catch (Exception ex) {
            ActivationStatus = LicenseActivationStatus.ActivationFailed;
            ReportException(ex);
            logger.LogError("[LICENSE] License activation failed. Exception type: {ExceptionType}", ex.GetType().Name);
        }
    }

    private async Task AddHeartbeatCoreAsync() {
        try {
            var heartbeatDto = new AddHeartbeatDto {
                Product_id = slasconeOptions.Value.ProductId,
                Client_id = ClientId,
                Software_version = _softwareVersion,
                Operating_system = deviceInfoService.GetOperatingSystem()
            };

            var slasconeClient = await SlasconeClient.Value;
            var result = await SlasconeErrorHandlingHelper.Execute(slasconeClient.Provisioning.AddHeartbeatAsync, heartbeatDto);

            if (null == result.data) {
                ReportError(result);

                if (result.error is { Id: (int)SlasconeApiErrorCode.UNKNOWN_CLIENT }) {
                    // A common error when the license is not activated yet for the current device.
                    // A typical handling could be to ask the user for a license key and call ActivateLicenseAsync.
                    logger.LogWarning("[LICENSE] The license has to be activated first.");
                    ClearLicenseState();
                }
                HeartbeatStatus = LicenseHeartbeatStatus.Failed;
                return;
            }

            var licenseInfoDto = result.data;

            if (await GetLicenseKeyFromSecretFile() != licenseInfoDto.License_key) { // This is a safety check to make sure that the license key used for heartbeat (the one used for activation) is the same as the one in the secret file. They should never be different so treat the heartbeat as failed, and log a warning, because this will almost guarantee 2006 unknown device error on open floating session.
                logger.LogWarning("[LICENSE] The license key used for heartbeat is different from the one in the secret file. This could indicate an issue with license activation or heartbeat.");
                ClearLicenseState();
                ActivationStatus = LicenseActivationStatus.ActivationFailed;
                HeartbeatStatus = LicenseHeartbeatStatus.Failed;
                return;
            }

            _tokenId = licenseInfoDto.Token_key;
            LimitationMap = licensePrinter.PrintLicenseDetails(licenseInfoDto);
            Publish(_state with {
                License = licenseInfoDto,
                Session = _state.License?.License_key == licenseInfoDto.License_key ? _state.Session : null,
                SessionStatus = _state.License?.License_key == licenseInfoDto.License_key ? _state.SessionStatus : LicenseSessionStatus.None,
                Heartbeat = LicenseHeartbeatStatus.Success,
                Activation = LicenseActivationStatus.Activated,
            });
            ClearLastError();
        }
        catch (Exception ex) {
            HeartbeatStatus = LicenseHeartbeatStatus.Failed;
            ReportException(ex);
            logger.LogError("[LICENSE] Adding heartbeat failed. Exception type: {ExceptionType}", ex.GetType().Name);
        }
    }

    private async Task UnassignLicenseCoreAsync() {
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

            ClearLicenseState();
            ClearLastError();
        }
        catch (Exception ex) {
            ReportException(ex);
            logger.LogError("[LICENSE] Unassigning license failed. Exception type: {ExceptionType}", ex.GetType().Name);
        }
    }

    public async Task AddAnalyticalHeartbeatAsync(Guid analyticaFieldId, string value) {
        var analyticalHeartbeatDto = new AnalyticalHeartbeatDto {
            Analytical_heartbeat = [],
            Client_id = ClientId
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
            ClearLastError();
        }
        catch (Exception ex) {
            ReportException(ex);
            logger.LogError("[LICENSE] Adding analytical heartbeat failed. Exception type: {ExceptionType}", ex.GetType().Name);
        }
    }

    public async Task AddUsageHeartbeatAsync(IEnumerable<(Guid usageFieldId, double value)> usages) {
        var usageHeartbeat = new FullUsageHeartbeatDto {
            Usage_heartbeat = [],
            Client_id = ClientId
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
            ClearLastError();
        }
        catch (Exception ex) {
            ReportException(ex);
            logger.LogError("[LICENSE] Adding usage heartbeat failed. Exception type: {ExceptionType}", ex.GetType().Name);
        }

    }

    public async Task AddConsumptionHeartbeatAsync(IEnumerable<(Guid, decimal)> consumptions) {
        var consumptionHeartbeat = new FullConsumptionHeartbeatDto {
            Client_id = ClientId,
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
                    logger.LogError("[LICENSE] Consumption for {Limitation} was not recorded: Limit reached!", limitation);
            }
            ClearLastError();
        }
        catch (Exception ex) {
            ReportException(ex);
            logger.LogError("[LICENSE] Adding consumption heartbeat failed. Exception type: {ExceptionType}", ex.GetType().Name);
        }
    }

    private async Task OpenSessionCoreAsync() {
        var licensekey = await GetLicenseKeyFromSecretFile();
        if (string.IsNullOrEmpty(licensekey)) {
            LastOpenSessionAttemptStatus = LicenseSessionStatus.OpenFailed;
            logger.LogWarning("[LICENSE] You have to add a license heartbeat first.");
            return;
        }
        if (LicenseInfo is { } acceptedLicense && acceptedLicense.License_key != licensekey) {
            ClearLicenseState();
            ActivationStatus = LicenseActivationStatus.ActivationFailed;
            LastOpenSessionAttemptStatus = LicenseSessionStatus.OpenFailed;
            logger.LogWarning("[LICENSE] Cannot open a session before the configured license is accepted.");
            return;
        }
        try {
            var sessionDto = new SessionRequestDto {
                Client_id = ClientId,
                License_id = Guid.Parse(licensekey),
                Session_id = _sessionId
            };

            var slasconeClient = await SlasconeClient.Value;
            var result = await SlasconeErrorHandlingHelper.Execute(slasconeClient.Provisioning.OpenSessionAsync, sessionDto);

            if (null == result.data) {
                ReportError(result);

                if (result.error is { Id: (int)SlasconeApiErrorCode.EXCEEDED_ALLOWED_CONNECTIONS }) {
                    // This error indicates that the maximum number of allowed parallel sessions has been reached.
                    // Normally you would inform the user about this and prevent the usage of the software.
                    logger.LogWarning("[LICENSE] Maximum of allowed parallel opened sessions exceeded!");
                }
                LastOpenSessionAttemptStatus = LicenseSessionStatus.OpenFailed;
                return;
            }

            var sessionStatus = result.data;
            if (licensekey != await GetLicenseKeyFromSecretFile()) {
                ClearLicenseState();
                ActivationStatus = LicenseActivationStatus.ActivationFailed;
                logger.LogWarning("[LICENSE] The configured license changed while opening a session; discarding the response.");
                return;
            }
            Publish(_state with { Session = sessionStatus, SessionStatus = LicenseSessionStatus.Opened });
            logger.LogInformation("[LICENSE] Session opened successfully {SessionId}", _sessionId);
            logger.LogInformation("[LICENSE] Max number of concurrent sessions: {MaxOpenSessionCount}", sessionStatus.Max_open_session_count);
            logger.LogInformation("[LICENSE] Session valid until {SessionValidUntil}", sessionStatus.Session_valid_until);
            ClearLastError();
        }
        catch (Exception ex) {
            LastOpenSessionAttemptStatus = LicenseSessionStatus.OpenFailed;
            ReportException(ex);
            logger.LogError("[LICENSE] Opening session failed. Exception type: {ExceptionType}", ex.GetType().Name);
        }
    }

    private async Task CloseSessionCoreAsync(CancellationToken cancellationToken) {
        var licensekey = LicenseInfo?.License_key ?? await GetLicenseKeyFromSecretFile().WaitAsync(cancellationToken);
        if (string.IsNullOrEmpty(licensekey)) {
            LastOpenSessionAttemptStatus = LicenseSessionStatus.CloseFailed;
            logger.LogError("[LICENSE] You have to add a license heartbeat first.");
            return;
        }

        try {
            var sessionDto = new SessionRequestDto {
                Client_id = ClientId,
                License_id = Guid.Parse(licensekey),
                Session_id = _sessionId
            };

            var slasconeClient = await SlasconeClient.Value.WaitAsync(cancellationToken);
            cancellationToken.ThrowIfCancellationRequested();
            Publish(_state with { Session = null });
            var result = await SlasconeErrorHandlingHelper.Execute(slasconeClient.Provisioning.CloseSessionAsync, sessionDto, cancellationToken: cancellationToken);

            if (null == result.data) {
                ReportError(result);
                LastOpenSessionAttemptStatus = LicenseSessionStatus.CloseFailed;
                return;
            }

            Publish(_state with { Session = null, SessionStatus = LicenseSessionStatus.Closed });
            ClearLastError();
            logger.LogInformation("[LICENSE] Session closed successfully {SessionId}", _sessionId);
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested) {
            LastOpenSessionAttemptStatus = LicenseSessionStatus.CloseFailed;
            logger.LogWarning("[LICENSE] Closing session was cancelled.");
            throw;
        }
        catch (Exception ex) {
            LastOpenSessionAttemptStatus = LicenseSessionStatus.CloseFailed;
            ReportException(ex);
            logger.LogError("[LICENSE] Closing session failed. Exception type: {ExceptionType}", ex.GetType().Name);
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
            logger.LogInformation("[LICENSE] Found {LicenseCount} license(s)", licenseDtos.Count);

            foreach (var licenseDto in licenseDtos) {
                licensePrinter.PrintLicenseDetails(licenseDto);
            }
            ClearLastError();
        }
        catch (Exception exception) {
            ReportException(exception);
            logger.LogError("[LICENSE] Looking up licenses failed. Exception type: {ExceptionType}", exception.GetType().Name);
        }
    }

    #region Private Methods

    private void ClearLicenseState() {
        _tokenId = null;
        LimitationMap = new Dictionary<Guid, string>();
        Publish(new());
    }

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
        caller = caller.Replace("CoreAsync", "Async", StringComparison.Ordinal);
        logger.LogError("[LICENSE] Error during {Caller}:", caller);
        var errorCode = MapErrorCode(result.error?.Id, result.errorType);
        var errorId = result.error?.Id ?? (int)errorCode;

        // LastError is surfaced through health diagnostics, so it only carries a locally composed message: provider/SDK text is never copied into it.
        LastError = new LicensingOperationError(
            caller,
            errorId,
            errorCode,
            $"The licensing operation failed with SLASCONE error code {errorId} ({errorCode}).",
            DateTimeOffset.UtcNow);

        // Provider/SDK error text and exception messages are untrusted and can carry keys or credentials: only the numeric error id and local category are logged.
        if (null != result.error)
            logger.LogError("[LICENSE] Error code: {ErrorId}", result.error.Id);

        logger.LogError("[LICENSE] Error type: {ErrorType}", result.errorType.ToString());
    }

    private void ReportException(Exception exception, [CallerMemberName] string caller = "")
        => LastError = new LicensingOperationError(
            caller.Replace("CoreAsync", "Async", StringComparison.Ordinal),
            (int)SlasconeApiErrorCode.API_CRASH,
            SlasconeApiErrorCode.API_CRASH,
            $"The licensing operation failed with exception type {exception.GetType().Name}.",
            DateTimeOffset.UtcNow);

    private void ClearLastError()
        => LastError = null;

    private static SlasconeApiErrorCode MapErrorCode(int? errorId, SlasconeErrorHandlingHelper.ErrorType errorType) {
        if (errorId.HasValue && Enum.IsDefined(typeof(SlasconeApiErrorCode), errorId.Value))
            return (SlasconeApiErrorCode)errorId.Value;

        if (errorType is SlasconeErrorHandlingHelper.ErrorType.Network)
            return SlasconeApiErrorCode.API_CRASH;

        return SlasconeApiErrorCode.UNKNOWN;
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
    private static LicenseComplianceStatus GetLicenseComplianceStatus(LicenseState state)
    => (state.License?.Is_license_valid ?? false)
    && state.Activation is LicenseActivationStatus.Activated
    && LicenseSessionValidity.IsValid(state.Session, DateTimeOffset.UtcNow)
    && state.Heartbeat is LicenseHeartbeatStatus.Success
        ? LicenseComplianceStatus.Compliant
        : LicenseComplianceStatus.NonCompliant;

    #endregion

}
