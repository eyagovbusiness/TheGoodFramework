
namespace TGF.CA.Infrastructure.Licensing.Slascone.Contracts {
    /// <summary>
    /// Defines methods for retrieving device-specific information, such as a unique device identifier, operating system
    /// details, and virtualization or cloud environment information.
    /// </summary>
    /// <remarks>Implementations of this interface provide platform-specific mechanisms to identify the device
    /// and its environment. This information is typically used for licensing, diagnostics, or environment-aware
    /// application behavior.</remarks>
    internal interface IDeviceInfoService {
        /// <summary>
        /// Gets a unique device identifier based on the system's hardware or cloud environment.
        /// This identifier is used to uniquely identify the device for licensing purposes.
        /// </summary>
        /// <returns>A string containing a unique device identifier.</returns>
        /// <summary>
        /// Get a unique device id based on the system
        /// </summary>
        /// <returns>UUID via string</returns>
        string GetUniqueDeviceId(bool skipCloudAndVirtualizationDetection = false);

        /// <summary>
        /// Gets information about the current operating system.
        /// Uses platform-specific methods to retrieve detailed OS information.
        /// </summary>
        /// <returns>A string containing the operating system information.</returns>
        string GetOperatingSystem();

        /// <summary>
        /// Gathers and formats information about virtualization and cloud environments.
        /// Detects if the application is running in AWS EC2, Azure VM, or another virtualized environment.
        /// </summary>
        /// <returns>A formatted string containing detailed information about the detected virtualization environment.</returns>
        string GetVirtualizationInfos();
    }
}