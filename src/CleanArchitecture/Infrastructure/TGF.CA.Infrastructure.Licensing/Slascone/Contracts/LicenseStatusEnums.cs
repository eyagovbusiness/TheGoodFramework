
namespace TGF.CA.Infrastructure.Licensing.Slascone.Contracts;

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

