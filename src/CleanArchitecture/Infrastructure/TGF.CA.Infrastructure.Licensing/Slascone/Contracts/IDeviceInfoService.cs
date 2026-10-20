
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
        /// Gets the Slascone license client identifier. Kubernetes deployments must prefer the configured
        /// LICENSE_CLIENT_ID value because cloud instance identifiers can change after node replacement and the historical Linux
        /// container fallback is based on the pod hostname, which changes on every restart. Those volatile identities leak
        /// Slascone activation seats and can make the license health check fail, causing probe-driven restart loops that burn
        /// additional seats. Cloud and OS-derived identifiers are intentionally retained as last-resort fallbacks so existing
        /// installations that upgrade without the new Secret keep their current activation identity.
        /// </summary>
        /// <returns>A string containing a unique device identifier.</returns>
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
