using Slascone.Client.DeviceInfos;
using System.Management;
using System.Runtime.InteropServices;
using System.Security.Cryptography;
using System.Text;
using TGF.CA.Infrastructure.Licensing.Slascone.Contracts;

namespace TGF.CA.Infrastructure.Licensing.Slascone.Services;

/// <summary>
/// Provides services for obtaining device-specific information used in license management.
/// Handles detection of device IDs, operating system information, and virtualization environments.
/// </summary>
internal class DeviceInfoService : IDeviceInfoService {
    private string? UniqueDeviceId { get; set; }

    public string GetUniqueDeviceId(bool skipCloudAndVirtualizationDetection = false) {
        if (!string.IsNullOrEmpty(UniqueDeviceId))
            return UniqueDeviceId;

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
                return UniqueDeviceId = awsEc2Infos.InstanceId;
            }
            if (azureDetected) {
                return UniqueDeviceId = azureVmInfos.VmId;
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

            return UniqueDeviceId;
        }

        if (RuntimeInformation.IsOSPlatform(OSPlatform.Linux)) {
            var deviceId = LinuxDeviceInfos.DockerEnvExists
                               ? LinuxDeviceInfos.Hostname
                               : string.Concat(LinuxDeviceInfos.MachineId, LinuxDeviceInfos.RootDeviceSerial);

            return UniqueDeviceId = BitConverter.ToString(MD5.HashData(UTF8Encoding.UTF8.GetBytes(deviceId)));
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