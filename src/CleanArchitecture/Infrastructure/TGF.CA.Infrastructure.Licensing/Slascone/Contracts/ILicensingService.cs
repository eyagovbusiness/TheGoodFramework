using Slascone.Client;
using Slascone.Client.Interfaces;

namespace TGF.CA.Infrastructure.Licensing.Slascone.Contracts;

/// <summary>
/// Defines the contract for managing software licensing, activation, compliance, and usage reporting with the SLASCONE
/// licensing service.
/// </summary>
/// <remarks>The ILicensingService interface provides methods and properties for interacting with the SLASCONE
/// licensing platform, including license activation, session management for floating licenses, compliance status
/// monitoring, and reporting of analytical, consumption, and usage data. Implementations are responsible for handling
/// communication with the SLASCONE service and maintaining up-to-date license state. All asynchronous operations return
/// tasks that complete when the corresponding operation with the licensing service has finished. Thread safety and
/// error handling depend on the specific implementation.</remarks>
public interface ILicensingService {

    /// <summary>
    /// Gets a map of limitation IDs to their corresponding names.
    /// </summary>
    IDictionary<Guid, string> LimitationMap { get; }
    /// <summary>
    /// Gets the SLASCONE client for version 2 of the SLASCONE API.
    /// </summary>
    Lazy<Task<ISlasconeClientV2>> SlasconeClient { get; }

    /// <summary>
    /// Gets the license information associated with the current instance.
    /// </summary>
    LicenseInfoDto? LicenseInfo { get; }

    /// <summary>
    /// Gets the current session status data, if available.
    /// </summary>
    SessionStatusDto? SessionInfo { get; }

    /// <summary>
    /// Gets the current activation status of the license on this device.
    /// </summary>
    LicenseActivationStatus ActivationStatus { get; }

    /// <summary>
    /// Gets the status of the last heartbeat sent to SLASCONE.
    /// </summary>
    LicenseHeartbeatStatus HeartbeatStatus { get; }

    /// <summary>
    /// Gets the result status of the most recent attempt to open a license floating session.
    /// </summary>
    /// <remarks>This property reflects only the outcome of the last open session attempt. It does not
    /// indicate the current state of the session. Use this property to diagnose issues when opening a session
    /// fails.</remarks>
    LicenseSessionStatus LastOpenSessionAttemptStatus { get; }

    /// <summary>
    /// Gets the current compliance status of the license.
    /// </summary>
    LicenseComplianceStatus ComplianceStatus { get; }

    /// <summary>
    /// Activates the software on the current device using the specified license key.
    /// </summary>
    /// <remarks>
    /// <para>
    /// Activation binds the license key to the unique hardware identity of the device, 
    /// enabling compliance verification and the acquisition of floating license seats.
    /// This process requires an active internet connection to communicate with the SLASCONE service.
    /// </para>
    /// <para>
    /// <b>Note:</b> If the device is already activated, calling this method will overwrite 
    /// existing activation data, effectively re-activating the device with the new license key.
    /// </para>
    /// </remarks>
    /// <returns>A <see cref="Task"/> representing the asynchronous activation operation.</returns>
    /// <exception cref="Exception">
    /// Thrown if the license key is invalid, already in use, or if a network error occurs.
    /// </exception>
    Task ActivateLicenseAsync();

    /// <summary>
    /// Sends an analytical heartbeat to the SLASCONE service.
    /// Used to track analytical field data for the current device.
    /// </summary>
    /// <param name="analyticaFieldId">The ID of the analytical field to update.</param>
    /// <param name="value">The value to report for the analytical field.</param>
    /// <returns>A task representing the asynchronous operation.</returns>
    Task AddAnalyticalHeartbeatAsync(Guid analyticaFieldId, string value);

    /// <summary>
    /// Sends consumption data to the SLASCONE service.
    /// Reports the consumption of limited resources to track usage against license limitations.
    /// </summary>
    /// <param name="consumptions">A collection of limitation IDs and their corresponding consumption values.</param>
    /// <returns>A task representing the asynchronous operation.</returns>
    Task AddConsumptionHeartbeatAsync(IEnumerable<(Guid, decimal)> consumptions);

    /// <summary>
    /// Sends a heartbeat to the SLASCONE service to validate the license.
    /// Updates license information based on the server response.
    /// </summary>
    /// <remarks>The method checks that the license key used for heartbeat (the one used for activation) is the same as the one in the secret file. If they are different, treat the heartbeat as failed and logs a warning. They should never be different so treat the heartbeat as failed, and log a warning because this will almost guarantee 2006 unknow device error on open floating session.</remarks>
    /// <returns>A task representing the asynchronous operation.</returns>
    Task AddHeartbeatAsync();

    /// <summary>
    /// Sends usage statistics to the SLASCONE service.
    /// Reports usage data for specified features.
    /// </summary>
    /// <param name="usages">A collection of usage field IDs and their corresponding values to report.</param>
    /// <returns>A task representing the asynchronous operation.</returns>
    Task AddUsageHeartbeatAsync(IEnumerable<(Guid usageFieldId, double value)> usages);
    /// <summary>
    /// Closes the most recently opened session with the SLASCONE service.
    /// Releases a floating license token for use by other clients.
    /// </summary>
    /// <returns>A task representing the asynchronous operation.</returns>
    Task CloseSessionAsync();

    /// <summary>
    /// Looks up licenses associated with the given license key.
    /// Logs details of found licenses.
    /// </summary>
    /// <returns>A task representing the asynchronous operation.</returns>
    Task LookupLicensesAsync();

    /// <summary>
    /// Opens a new session with the SLASCONE service.
    /// Used for floating licenses to track concurrent usage.
    /// </summary>
    /// <returns>A task representing the asynchronous operation.</returns>
    Task OpenSessionAsync();

    /// <summary>
    /// Unassigns the license from the current device.
    /// Requires a token key from a previous license activation or heartbeat.
    /// </summary>
    /// <returns>A task representing the asynchronous operation.</returns>
    Task UnassignLicenseAsync();

}
