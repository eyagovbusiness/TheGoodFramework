using Microsoft.Extensions.Logging.Abstractions;
using TGF.CA.Infrastructure.InvariantConstants;
using TGF.CA.Infrastructure.Licensing.Slascone.Services;
using TGF.CA.Infrastructure.Licensing.Tests.Unit.TestSupport;

namespace TGF.CA.Infrastructure.Licensing.Tests.Unit.Infrastructure;

public class DeviceInfoServiceTests {
    private const string SentinelClientId = "sentinel-client-id-7f3a9c1e";

    [Fact]
    public void GetUniqueDeviceId_ShouldReturnTrimmedConfiguredLicenseClientId() {
        var previousValue = Environment.GetEnvironmentVariable(EnvironmentVariableNames.LICENSE_CLIENT_ID);
        try {
            Environment.SetEnvironmentVariable(EnvironmentVariableNames.LICENSE_CLIENT_ID, "  configured-client-id  ");
            var sut = new DeviceInfoService(NullLogger<DeviceInfoService>.Instance);

            var result = sut.GetUniqueDeviceId(skipCloudAndVirtualizationDetection: true);

            Assert.Equal("configured-client-id", result);
        }
        finally {
            Environment.SetEnvironmentVariable(EnvironmentVariableNames.LICENSE_CLIENT_ID, previousValue);
        }
    }

    [Fact]
    public void GetUniqueDeviceId_ShouldReturnAndCacheConfiguredIdWithoutLoggingTheIdentifier() {
        var previousValue = Environment.GetEnvironmentVariable(EnvironmentVariableNames.LICENSE_CLIENT_ID);
        try {
            Environment.SetEnvironmentVariable(EnvironmentVariableNames.LICENSE_CLIENT_ID, $"  {SentinelClientId}  ");
            var logger = new CapturingLogger<DeviceInfoService>();
            var sut = new DeviceInfoService(logger);

            var first = sut.GetUniqueDeviceId(skipCloudAndVirtualizationDetection: true);
            // The configured id is cached: a later change of the variable must not change the id used for the whole process lifetime.
            Environment.SetEnvironmentVariable(EnvironmentVariableNames.LICENSE_CLIENT_ID, "changed-after-first-resolution");
            var second = sut.GetUniqueDeviceId(skipCloudAndVirtualizationDetection: true);

            Assert.Equal(SentinelClientId, first);
            Assert.Equal(SentinelClientId, second);
            var entry = Assert.Single(logger.Entries);
            Assert.Contains(EnvironmentVariableNames.LICENSE_CLIENT_ID, entry.Message);
            Assert.All(logger.AllObservableText(), text => {
                Assert.DoesNotContain(SentinelClientId, text);
                Assert.DoesNotContain("changed-after-first-resolution", text);
            });
        }
        finally {
            Environment.SetEnvironmentVariable(EnvironmentVariableNames.LICENSE_CLIENT_ID, previousValue);
        }
    }

    /// <summary>
    /// The AWS/Azure/Windows/Linux resolution branches need real IMDS, WMI or OS calls and cannot run hermetically, so they are guarded
    /// structurally: no log call in the service may reference the resolved identifier or the raw cloud/OS identity values.
    /// </summary>
    [Fact]
    public void DeviceInfoServiceSource_ShouldNotPassAnyIdentifierToLogCalls() {
        var source = File.ReadAllText(LocateSourceFile());
        var logCalls = System.Text.RegularExpressions.Regex
            .Matches(source, @"_logger\s*\.\s*Log\w*\s*\((?<arguments>.*?)\)\s*;", System.Text.RegularExpressions.RegexOptions.Singleline)
            .Select(match => match.Groups["arguments"].Value)
            .ToList();

        // Configured variable, AWS, Azure, Windows fallback and Linux fallback.
        Assert.True(logCalls.Count >= 5, $"Expected at least five log calls, found {logCalls.Count}.");
        Assert.All(logCalls, arguments => {
            Assert.DoesNotContain("ClientId}", arguments);
            Assert.DoesNotContain("UniqueDeviceId", arguments);
            Assert.DoesNotContain("InstanceId", arguments);
            Assert.DoesNotContain("VmId", arguments);
            Assert.DoesNotContain("configuredClientId", arguments);
            Assert.DoesNotContain("deviceId", arguments);
        });
    }

    [Fact(Skip = "Whitespace LICENSE_CLIENT_ID falls through to lower device-id tiers, which are not hermetic on Windows because they can invoke WMI. Tier 2/3 fallback validation is intentionally skipped.")]
    public void GetUniqueDeviceId_ShouldFallThroughWhenConfiguredLicenseClientIdIsWhitespace()
        => throw new NotSupportedException("Cannot validate whitespace fallthrough without invoking non-hermetic lower device-id tiers.");

    private static string LocateSourceFile() {
        var relative = Path.Combine("src", "CleanArchitecture", "Infrastructure", "TGF.CA.Infrastructure.Licensing", "Slascone", "Services", "DeviceInfoService.cs");
        for (var directory = new DirectoryInfo(AppContext.BaseDirectory); directory is not null; directory = directory.Parent) {
            var candidate = Path.Combine(directory.FullName, relative);
            if (File.Exists(candidate))
                return candidate;
        }

        throw new FileNotFoundException($"Could not locate {relative} above {AppContext.BaseDirectory}.");
    }
}
