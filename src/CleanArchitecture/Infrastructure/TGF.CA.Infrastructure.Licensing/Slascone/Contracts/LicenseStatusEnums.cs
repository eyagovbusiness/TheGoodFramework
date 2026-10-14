
namespace TGF.CA.Infrastructure.Licensing.Slascone.Contracts;

/// <summary>
/// Represents SLASCONE API error codes returned by licensing operations.
/// </summary>
/// <remarks>
/// See https://support.slascone.com/hc/en-us/articles/360016160398-ERROR-CODES.
/// </remarks>
public enum SlasconeApiErrorCode {
    /// <summary>
    /// No error occurred.
    /// </summary>
    NONE = 0,

    /// <summary>
    /// The supplied license key is invalid.
    /// </summary>
    INVALID_KEY = 1000,

    /// <summary>
    /// The supplied license key has expired.
    /// </summary>
    EXPIRED_KEY = 1001,

    /// <summary>
    /// The license has not been activated.
    /// </summary>
    NOT_ACTIVATED = 1002,

    /// <summary>
    /// The running software version is not compliant with the license.
    /// </summary>
    NON_COMPLIANT_VERSION = 1003,

    /// <summary>
    /// The maximum number of allowed concurrent connections or sessions has been exceeded.
    /// </summary>
    EXCEEDED_ALLOWED_CONNECTIONS = 1007,

    /// <summary>
    /// The token is already assigned to another device.
    /// </summary>
    TOKEN_ALREADY_ASSINGED = 2001,

    /// <summary>
    /// The client device is unknown to SLASCONE for this license.
    /// </summary>
    UNKNOWN_CLIENT = 2006,

    /// <summary>
    /// The SLASCONE API failed or could not be reached by the local licensing operation.
    /// </summary>
    API_CRASH = 9998,

    /// <summary>
    /// The error code is unknown or was not supplied.
    /// </summary>
    UNKNOWN = 9999
}

/// <summary>
/// Captures the most recent failed licensing operation.
/// </summary>
/// <param name="OperationName">The name of the licensing operation that failed.</param>
/// <param name="Code">The numeric SLASCONE error code, or a local mapped code for network-class failures.</param>
/// <param name="ErrorCode">The mapped SLASCONE API error code.</param>
/// <param name="Message">A human-readable error message without license keys or other secrets.</param>
/// <param name="UtcTimestamp">The UTC timestamp when the failure was recorded.</param>
public sealed record LicensingOperationError(
    string OperationName,
    int Code,
    SlasconeApiErrorCode ErrorCode,
    string Message,
    DateTimeOffset UtcTimestamp);

/// <summary>
/// Represents the status of the last license heartbeat operation.
/// </summary>
public enum LicenseHeartbeatStatus {
    /// <summary>
    /// No heartbeat has been sent yet.
    /// </summary>
    None,

    /// <summary>
    /// The last heartbeat was successful.
    /// </summary>
    Success,

    /// <summary>
    /// The last heartbeat failed (e.g., network error or license compliance issue).
    /// </summary>
    Failed
}

/// <summary>
/// Represents the current state of the floating license session.
/// </summary>
public enum LicenseSessionStatus {
    /// <summary>
    /// No session has been opened yet.
    /// </summary>
    None,

    /// <summary>
    /// A floating license session is currently open.
    /// </summary>
    Opened,

    /// <summary>
    /// The floating license session has been closed.
    /// </summary>
    Closed,

    /// <summary>
    /// An attempt to open a floating license session failed.
    /// </summary>
    OpenFailed,

    /// <summary>
    /// An attempt to close a floating license session failed.
    /// </summary>
    CloseFailed
}

/// <summary>
/// Represents the activation state of the license on this device.
/// </summary>
public enum LicenseActivationStatus {
    /// <summary>
    /// The license is not currently assigned to this device (never activated or was unassigned).
    /// </summary>
    Unassigned,

    /// <summary>
    /// The license has been successfully activated.
    /// </summary>
    Activated,

    /// <summary>
    /// The license activation attempt failed.
    /// </summary>
    ActivationFailed,
}

/// <summary>
/// Represents the compliance state of the license according to SLASCONE.
/// </summary>
public enum LicenseComplianceStatus {
    Compliant,
    NonCompliant,

    ///// <summary>
    ///// The compliance state is unknown (e.g., before first heartbeat).
    ///// </summary>
    //Unknown,

    ///// <summary>
    ///// The license is active and compliant.
    ///// </summary>
    //Active,

    ///// <summary>
    ///// The license is suspended and cannot be used.
    ///// </summary>
    //Suspended,

    ///// <summary>
    ///// The license has expired and is no longer valid.
    ///// </summary>
    //Expired,

    ///// <summary>
    ///// The license is inactive due to strict heartbeat mode (heartbeat overdue).
    ///// </summary>
    //Inactive
}
