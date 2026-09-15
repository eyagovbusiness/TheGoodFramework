using Microsoft.Extensions.Logging;
using Slascone.Client.DeviceInfos;
using System.Management;
using System.Runtime.InteropServices;
using System.Security.Cryptography;
using System.Text;
using TGF.CA.Infrastructure.InvariantConstants;
using TGF.CA.Infrastructure.Licensing.Slascone.Contracts;

namespace TGF.CA.Infrastructure.Licensing.Slascone.Services;

/// <summary>
/// Provides services for obtaining device-specific information used in license management.
/// Handles detection of device IDs, operating system information, and virtualization environments.
/// </summary>
internal class DeviceInfoService : IDeviceInfoService {
    private readonly ILogger<DeviceInfoService> _logger;
    private string? UniqueDeviceId { get; set; }

    public DeviceInfoService(ILogger<DeviceInfoService> logger) {
        _logger = logger;
    }

    /// <summary>
    /// Gets the Slascone license client identifier. Kubernetes deployments must prefer the configured
    /// <see cref="EnvironmentVariableNames.LICENSE_CLIENT_ID"/> value because cloud instance identifiers can change after node
    /// replacement and the historical Linux container fallback is based on the pod hostname, which changes on every restart.
    /// Those volatile identities leak Slascone activation seats and can make the license health check fail, causing probe-driven
    /// restart loops that burn additional seats. Cloud and OS-derived identifiers are intentionally retained as last-resort
    /// fallbacks so existing installations that upgrade without the new Secret keep their current activation identity.
    /// </summary>
    public string GetUniqueDeviceId(bool skipCloudAndVirtualizationDetection = false) {
        if (!string.IsNullOrEmpty(UniqueDeviceId))
            return UniqueDeviceId;

        var configuredClientId = Environment.GetEnvironmentVariable(EnvironmentVariableNames.LICENSE_CLIENT_ID);
        if (!string.IsNullOrWhiteSpace(configuredClientId)) {
            UniqueDeviceId = configuredClientId.Trim();
            _logger.LogInformation("[LICENSE] Client id resolved from configured environment variable {EnvironmentVariableName}: {ClientId}", EnvironmentVariableNames.LICENSE_CLIENT_ID, UniqueDeviceId);
            return UniqueDeviceId;
        }

        if (!skipCloudAndVirtualizationDetection) {
            var awsEc2Infos = new AwsEc2Infos() { TimeoutSeconds = 2 };
            var detectAws = new Task<bool>(() => awsEc2Infos.DetectAwsEcs().Result);
            detectAws.Start();
            var azureVmInfos = new AzureVmInfos() { TimeoutSeconds = 2 };
            var detectAzure = new Task<bool>(() => azureVmInfos.DetectAzureVm().Result);
            detectAzure.Start();
            var virtualizationInfos = new VirtualizationInfos();
            var detectVirtualization = new Task<bool>(() => virtualizationInfos.DetectVirtualization().Result);
            detectVirtualization.Start();

            Task.WaitAll(detectAws, detectAzure, detectVirtualization);

            var awsDetected = detectAws.Result;
            var azureDetected = detectAzure.Result;
            var virtualizationDetected = detectVirtualization.Result;

            if (awsDetected) {
                UniqueDeviceId = awsEc2Infos.InstanceId;
                _logger.LogInformation("[LICENSE] Client id resolved from AWS IMDS instance id: {ClientId}", UniqueDeviceId);
                return UniqueDeviceId;
            }
            if (azureDetected) {
                UniqueDeviceId = azureVmInfos.VmId;
                _logger.LogInformation("[LICENSE] Client id resolved from Azure IMDS VM id: {ClientId}", UniqueDeviceId);
                return UniqueDeviceId;
            }
        }

        if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows)) {
            try {
                UniqueDeviceId = WindowsDeviceInfos.ComputerSystemProductId;
            }
            catch (ManagementException managementException) {
                // WindowsDeviceInfos.ComputerSystemProductId uses a WMI query to get the machine ID
                // If a problem occurs executing the WMI query a device id has to be created in an alternative way
                _ = managementException;
                UniqueDeviceId = $"{Guid.NewGuid()}-fallback";
            }

            _logger.LogInformation("[LICENSE] Client id resolved from Windows device information fallback: {ClientId}", UniqueDeviceId);
            return UniqueDeviceId;
        }

        if (RuntimeInformation.IsOSPlatform(OSPlatform.Linux)) {
            var deviceId = LinuxDeviceInfos.DockerEnvExists
                               ? LinuxDeviceInfos.Hostname
                               : string.Concat(LinuxDeviceInfos.MachineId, LinuxDeviceInfos.RootDeviceSerial);

            UniqueDeviceId = BitConverter.ToString(MD5.HashData(UTF8Encoding.UTF8.GetBytes(deviceId)));
            _logger.LogInformation("[LICENSE] Client id resolved from Linux device information fallback: {ClientId}", UniqueDeviceId);
            return UniqueDeviceId;
        }

        throw new NotSupportedException("GetUniqueDeviceId() is supported only on Windows and Linux");
    }

    public string GetOperatingSystem()
        => RuntimeInformation.IsOSPlatform(OSPlatform.Windows)
            ? WindowsDeviceInfos.OperatingSystem
            : RuntimeInformation.IsOSPlatform(OSPlatform.Linux) ? LinuxDeviceInfos.OSVersion : RuntimeInformation.OSDescription;

    public string GetVirtualizationInfos() {
        var sb = new StringBuilder();

        var awsEc2Infos = new AwsEc2Infos() { TimeoutSeconds = 2 };
        var detectAws = new Task<bool>(() => awsEc2Infos.DetectAwsEcs().Result);
        detectAws.Start();
        var azureVmInfos = new AzureVmInfos() { TimeoutSeconds = 2 };
        var detectAzure = new Task<bool>(() => azureVmInfos.DetectAzureVm().Result);
        detectAzure.Start();
        var virtualizationInfos = new VirtualizationInfos();
        var detectVirtualization = new Task<bool>(() => virtualizationInfos.DetectVirtualization().Result);
        detectVirtualization.Start();

        Task.WaitAll(detectAws, detectAzure, detectVirtualization);

        var awsEc2Detected = detectAws.Result;
        var azureVmDetected = detectAzure.Result;
        var virtualizationDetected = detectVirtualization.Result;

        if (awsEc2Detected) {
            sb.AppendLine("Running on an AWS EC2 instance:");
            sb.AppendLine($"    Instance Id: {awsEc2Infos.InstanceId}");
            sb.AppendLine($"    Instance Type: {awsEc2Infos.InstanceType}");
            sb.AppendLine($"    Instance Region: {awsEc2Infos.Region}");
            sb.AppendLine($"    Instance Version: {awsEc2Infos.Version}");
        }

        if (azureVmDetected) {
            sb.AppendLine("Running on an Azure VM.");
            sb.AppendLine($"    Name: {azureVmInfos.Name}");
            sb.AppendLine($"    Vm Id: {azureVmInfos.VmId}");
            sb.AppendLine($"    Resource Id: {azureVmInfos.ResourceId}");
            sb.AppendLine($"    Location: {azureVmInfos.Location}");
            sb.AppendLine($"    Version: {azureVmInfos.Version}");
            sb.AppendLine($"    Provider: {azureVmInfos.Provider}");
            sb.AppendLine($"    Publisher: {azureVmInfos.Publisher}");
            sb.AppendLine($"    Vm size: {azureVmInfos.VmSize}");
            sb.AppendLine($"    License type: {azureVmInfos.LicenseType}");
        }

        if (virtualizationDetected) {
            sb.AppendLine($"Virtualization detected: {virtualizationInfos.VirtualizationType}");
        }

        if (!awsEc2Detected && !azureVmDetected && !virtualizationDetected) {
            sb.AppendLine("No virtualization or cloud environment detected.");
        }

        return sb.ToString();
    }
}
