using Microsoft.Extensions.Logging.Abstractions;
using TGF.CA.Infrastructure.InvariantConstants;
using TGF.CA.Infrastructure.Licensing.Slascone.Services;

namespace TGF.CA.Infrastructure.Licensing.Tests.Unit.Infrastructure;

public class DeviceInfoServiceTests {
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

    [Fact(Skip = "Whitespace LICENSE_CLIENT_ID falls through to lower device-id tiers, which are not hermetic on Windows because they can invoke WMI. Tier 2/3 fallback validation is intentionally skipped.")]
    public void GetUniqueDeviceId_ShouldFallThroughWhenConfiguredLicenseClientIdIsWhitespace()
        => throw new NotSupportedException("Cannot validate whitespace fallthrough without invoking non-hermetic lower device-id tiers.");
}
