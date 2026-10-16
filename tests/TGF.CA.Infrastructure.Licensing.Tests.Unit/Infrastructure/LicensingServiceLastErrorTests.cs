using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Moq;
using Slascone.Client;
using Slascone.Client.Interfaces;
using Slascone.Client.Xml;
using TGF.CA.Infrastructure.InvariantConstants;
using TGF.CA.Infrastructure.Licensing.Slascone;
using TGF.CA.Infrastructure.Licensing.Slascone.Contracts;
using TGF.CA.Infrastructure.Licensing.Slascone.Helpers;
using TGF.CA.Infrastructure.Licensing.Slascone.Services;
using TGF.CA.Infrastructure.Licensing.Tests.Unit.TestSupport;
using TGF.CA.Infrastructure.Secrets.SecretsFiles;

namespace TGF.CA.Infrastructure.Licensing.Tests.Unit.Infrastructure;

/// <summary>
/// The last licensing error is surfaced through health diagnostics and logs, so provider/SDK text (error messages, help links and exception
/// messages, which can echo keys or credentials) must never be copied into it or logged. Only the numeric code, the mapped enum name and the
/// exception type name are allowed.
/// </summary>
public class LicensingServiceLastErrorTests {
    private const string SecretText = "super-secret-provider-text-11111111-2222-3333-4444-555555555555";

    [Fact]
    public async Task ActivateLicenseAsync_ShouldNotLeakExceptionMessageIntoLastErrorOrLogs() {
        using var environment = SoftwareVersionScope.Set();
        var logger = new CapturingLogger<LicensingService>();
        var sut = CreateSut(logger, Task.FromException<ISlasconeClientV2>(new InvalidOperationException(SecretText)));

        await sut.ActivateLicenseAsync();

        Assert.Equal(LicenseActivationStatus.ActivationFailed, sut.ActivationStatus);
        Assert.NotNull(sut.LastError);
        Assert.Equal(SlasconeApiErrorCode.API_CRASH, sut.LastError!.ErrorCode);
        Assert.DoesNotContain(SecretText, sut.LastError.Message);
        Assert.Contains(nameof(InvalidOperationException), sut.LastError.Message);
        Assert.NotEmpty(logger.Entries);
        Assert.All(logger.Entries, entry => Assert.Null(entry.Exception));
        Assert.All(logger.AllObservableText(), text => Assert.DoesNotContain(SecretText, text));
        Assert.Contains(logger.Entries, entry => entry.Message.Contains(nameof(InvalidOperationException)));
    }

    [Fact]
    public async Task ActivateLicenseAsync_ShouldNotLeakProviderErrorTupleTextIntoLastErrorOrLogs() {
        using var environment = SoftwareVersionScope.Set();
        var logger = new CapturingLogger<LicensingService>();
        var provisioning = new Mock<ISlasconeProvisioningClientV2>();
        provisioning
            .Setup(client => client.ActivateLicenseAsync(It.IsAny<ActivateClientDto>()))
            .ReturnsAsync(new ApiResponse<LicenseInfoDto> {
                // A 409 makes the helper return both the provider error object and a composed tuple message that embeds the provider text.
                StatusCode = 409,
                Error = new ErrorResultObjects { Id = (int)SlasconeApiErrorCode.EXCEEDED_ALLOWED_CONNECTIONS, Message = SecretText, Help = SecretText }
            });
        var client = new Mock<ISlasconeClientV2>();
        client.SetupGet(slasconeClient => slasconeClient.Provisioning).Returns(provisioning.Object);
        var sut = CreateSut(logger, Task.FromResult(client.Object));

        await sut.ActivateLicenseAsync();

        Assert.NotNull(sut.LastError);
        Assert.Equal(SlasconeApiErrorCode.EXCEEDED_ALLOWED_CONNECTIONS, sut.LastError!.ErrorCode);
        Assert.Equal((int)SlasconeApiErrorCode.EXCEEDED_ALLOWED_CONNECTIONS, sut.LastError.Code);
        Assert.DoesNotContain(SecretText, sut.LastError.Message);
        Assert.DoesNotContain(SecretText, sut.LastError.OperationName);
        Assert.NotEmpty(logger.Entries);
        Assert.All(logger.Entries, entry => Assert.Null(entry.Exception));
        Assert.All(logger.AllObservableText(), text => Assert.DoesNotContain(SecretText, text));
        Assert.Contains(logger.AllObservableText(), text => text.Contains(((int)SlasconeApiErrorCode.EXCEEDED_ALLOWED_CONNECTIONS).ToString()));
    }

    [Fact]
    public async Task ActivateLicenseAsync_ShouldNotLeakExceptionThrownByProviderCallIntoLastErrorOrLogs() {
        using var environment = SoftwareVersionScope.Set();
        var logger = new CapturingLogger<LicensingService>();
        var provisioning = new Mock<ISlasconeProvisioningClientV2>();
        provisioning
            .Setup(client => client.ActivateLicenseAsync(It.IsAny<ActivateClientDto>()))
            .ThrowsAsync(new HttpRequestException(SecretText));
        var client = new Mock<ISlasconeClientV2>();
        client.SetupGet(slasconeClient => slasconeClient.Provisioning).Returns(provisioning.Object);
        var sut = CreateSut(logger, Task.FromResult(client.Object));

        await sut.ActivateLicenseAsync();

        Assert.NotNull(sut.LastError);
        Assert.DoesNotContain(SecretText, sut.LastError!.Message);
        Assert.All(logger.Entries, entry => Assert.Null(entry.Exception));
        Assert.All(logger.AllObservableText(), text => Assert.DoesNotContain(SecretText, text));
    }

    private static LicensingService CreateSut(ILogger<LicensingService> logger, Task<ISlasconeClientV2> clientTask) {
        var clientFactory = new Mock<ISlasconeClientFactory>();
        clientFactory.Setup(factory => factory.GetClientAsync()).Returns(clientTask);

        var secretFiles = new Mock<ISecretFilesService>();
        secretFiles.Setup(service => service.GetSecretFromConfigAsync(It.IsAny<string>())).ReturnsAsync("11111111-2222-3333-4444-555555555555");

        return new LicensingService(
            clientFactory.Object,
            secretFiles.Object,
            new NoOpLicensePrinter(),
            new FixedDeviceInfoService(),
            Options.Create(new SlasconeOptions { ApiBaseUrl = "https://slascone.invalid", IsvId = Guid.Empty, ProductId = Guid.Empty }),
            logger,
            new ConfigurationBuilder().Build());
    }

    private sealed class SoftwareVersionScope : IDisposable {
        private readonly string? _previous = Environment.GetEnvironmentVariable(EnvironmentVariableNames.SOFTWARE_VERSION);

        public static SoftwareVersionScope Set() {
            var scope = new SoftwareVersionScope();
            Environment.SetEnvironmentVariable(EnvironmentVariableNames.SOFTWARE_VERSION, "1.0.0-test");
            return scope;
        }

        public void Dispose() => Environment.SetEnvironmentVariable(EnvironmentVariableNames.SOFTWARE_VERSION, _previous);
    }

    private sealed class FixedDeviceInfoService : IDeviceInfoService {
        public string GetUniqueDeviceId(bool skipCloudAndVirtualizationDetection = false) => "test-client-id";
        public string GetOperatingSystem() => "test-os";
        public string GetVirtualizationInfos() => string.Empty;
    }

    private sealed class NoOpLicensePrinter : ISlasconeLicensePrinter {
        public IDictionary<Guid, string> PrintLicenseDetails(LicenseDto license) => new Dictionary<Guid, string>();
        public void PrintLicenseDetails(LicenseXmlDto licenseXml) { }
        public IDictionary<Guid, string> PrintLicenseDetails(LicenseInfoDto licenseInfo) => new Dictionary<Guid, string>();
    }
}
