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

    private static LicensingService CreateSut(ILogger<LicensingService> logger, Task<ISlasconeClientV2> clientTask, ISecretFilesService? secretFilesOverride = null) {
        var clientFactory = new Mock<ISlasconeClientFactory>();
        clientFactory.Setup(factory => factory.GetClientAsync()).Returns(clientTask);

        var secretFiles = new Mock<ISecretFilesService>();
        secretFiles.Setup(service => service.GetSecretFromConfigAsync(It.IsAny<string>())).ReturnsAsync("11111111-2222-3333-4444-555555555555");

        return new LicensingService(
            clientFactory.Object,
            secretFilesOverride ?? secretFiles.Object,
            new NoOpLicensePrinter(),
            new FixedDeviceInfoService(),
            Options.Create(new SlasconeOptions { ApiBaseUrl = "https://slascone.invalid", IsvId = Guid.Empty, ProductId = Guid.Empty }),
            logger,
            new ConfigurationBuilder().Build());
    }

    [Fact]
    public async Task ActivateLicenseAsync_PublishesAcceptedSnapshot() {
        using var environment = SoftwareVersionScope.Set();
        var (sut, provisioning) = CreateProvisioningSut();
        var license = LicenseSnapshot();
        provisioning.Setup(client => client.ActivateLicenseAsync(It.IsAny<ActivateClientDto>()))
            .ReturnsAsync(new ApiResponse<LicenseInfoDto> { StatusCode = 200, Result = license });
        provisioning.Setup(client => client.AddHeartbeatAsync(It.IsAny<AddHeartbeatDto>()))
            .ReturnsAsync(new ApiResponse<LicenseInfoDto> { StatusCode = 200, Result = LicenseSnapshot() });
        await sut.AddHeartbeatAsync();
        Assert.Equal(LicenseHeartbeatStatus.Success, sut.HeartbeatStatus);

        await sut.ActivateLicenseAsync();

        Assert.Same(license, sut.LicenseInfo);
        Assert.Equal(LicenseActivationStatus.Activated, sut.ActivationStatus);
        Assert.Equal(LicenseHeartbeatStatus.None, sut.HeartbeatStatus);
        Assert.Equal(LicenseComplianceStatus.NonCompliant, sut.ComplianceStatus);
    }

    [Fact]
    public async Task ActivateLicenseAsync_RejectsWrongLicense() {
        using var environment = SoftwareVersionScope.Set();
        var (sut, provisioning) = CreateProvisioningSut();
        var license = LicenseSnapshot();
        license.License_key = "wrong-license";
        provisioning.Setup(client => client.ActivateLicenseAsync(It.IsAny<ActivateClientDto>()))
            .ReturnsAsync(new ApiResponse<LicenseInfoDto> { StatusCode = 200, Result = license });

        await sut.ActivateLicenseAsync();

        Assert.Null(sut.LicenseInfo);
        Assert.Equal(LicenseActivationStatus.ActivationFailed, sut.ActivationStatus);
    }

    [Fact]
    public async Task AddHeartbeatAsync_RejectsWrongLicense_AndClearsPriorSnapshot() {
        using var environment = SoftwareVersionScope.Set();
        var (sut, provisioning) = CreateProvisioningSut();
        var accepted = LicenseSnapshot();
        var rejected = LicenseSnapshot();
        rejected.License_key = "wrong-license";
        provisioning.SetupSequence(client => client.AddHeartbeatAsync(It.IsAny<AddHeartbeatDto>()))
            .ReturnsAsync(new ApiResponse<LicenseInfoDto> { StatusCode = 200, Result = accepted })
            .ReturnsAsync(new ApiResponse<LicenseInfoDto> { StatusCode = 200, Result = rejected });

        await sut.AddHeartbeatAsync();
        Assert.Same(accepted, sut.LicenseInfo);
        await sut.AddHeartbeatAsync();

        Assert.Null(sut.LicenseInfo);
        Assert.Equal(LicenseHeartbeatStatus.Failed, sut.HeartbeatStatus);
        Assert.Equal(LicenseComplianceStatus.NonCompliant, sut.ComplianceStatus);
    }

    [Fact]
    public async Task AddHeartbeatAsync_RefreshesSnapshot_AndFailureCannotRemainCompliant() {
        using var environment = SoftwareVersionScope.Set();
        var (sut, provisioning) = CreateProvisioningSut();
        var first = LicenseSnapshot();
        var next = LicenseSnapshot();
        provisioning.SetupSequence(client => client.AddHeartbeatAsync(It.IsAny<AddHeartbeatDto>()))
            .ReturnsAsync(new ApiResponse<LicenseInfoDto> { StatusCode = 200, Result = first })
            .ReturnsAsync(new ApiResponse<LicenseInfoDto> { StatusCode = 200, Result = next })
            .ReturnsAsync(new ApiResponse<LicenseInfoDto> { StatusCode = 403 });

        await sut.AddHeartbeatAsync();
        Assert.Same(first, sut.LicenseInfo);
        await sut.AddHeartbeatAsync();
        Assert.Same(next, sut.LicenseInfo);
        await sut.AddHeartbeatAsync();
        Assert.Same(next, sut.LicenseInfo);
        Assert.Equal(LicenseHeartbeatStatus.Failed, sut.HeartbeatStatus);
        Assert.Equal(LicenseComplianceStatus.NonCompliant, sut.ComplianceStatus);
    }

    [Fact]
    public async Task UnassignLicenseAsync_ClearsPublishedLicenseAndStatuses() {
        using var environment = SoftwareVersionScope.Set();
        var (sut, provisioning) = CreateProvisioningSut();
        provisioning.Setup(client => client.AddHeartbeatAsync(It.IsAny<AddHeartbeatDto>()))
            .ReturnsAsync(new ApiResponse<LicenseInfoDto> { StatusCode = 200, Result = LicenseSnapshot() });
        provisioning.Setup(client => client.UnassignLicenseAsync(It.IsAny<UnassignDto>()))
            .ReturnsAsync(new ApiResponse<string> { StatusCode = 200, Result = "OK" });
        await sut.AddHeartbeatAsync();
        await sut.UnassignLicenseAsync();

        Assert.Null(sut.LicenseInfo);
        Assert.Null(sut.SessionInfo);
        Assert.Empty(sut.LimitationMap);
        Assert.Equal(LicenseActivationStatus.Unassigned, sut.ActivationStatus);
        Assert.Equal(LicenseHeartbeatStatus.None, sut.HeartbeatStatus);
        Assert.Equal(LicenseComplianceStatus.NonCompliant, sut.ComplianceStatus);
    }

    private static LicenseInfoDto LicenseSnapshot() => new() {
        License_key = "11111111-2222-3333-4444-555555555555",
        Token_key = Guid.NewGuid(),
        Is_license_valid = true,
        Features = [],
    };

    [Fact]
    public async Task CloseSessionAsync_ClearsSessionAndCompliance_ButRetainsLicense() {
        using var environment = SoftwareVersionScope.Set();
        var (sut, provisioning) = await CreateCompliantSutAsync();
        var before = sut.Snapshot;
        provisioning.Setup(client => client.CloseSessionAsync(It.IsAny<SessionRequestDto>()))
            .ReturnsAsync(new ApiResponse<string> { StatusCode = 200, Result = "OK" });

        await sut.CloseSessionAsync();

        Assert.Null(sut.SessionInfo);
        Assert.Equal(LicenseSessionStatus.Closed, sut.LastOpenSessionAttemptStatus);
        Assert.Equal(LicenseComplianceStatus.NonCompliant, sut.Snapshot.ComplianceStatus);
        Assert.Same(before.LicenseInfo, sut.Snapshot.LicenseInfo);
        Assert.Equal(LicenseComplianceStatus.Compliant, before.ComplianceStatus);
    }

    [Fact]
    public async Task ActivationInFlight_PublishesNonCompliantSnapshot_UntilFreshHeartbeatAndSession() {
        using var environment = SoftwareVersionScope.Set();
        var (sut, provisioning) = await CreateCompliantSutAsync();
        var entered = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var response = new TaskCompletionSource<ApiResponse<LicenseInfoDto>>(TaskCreationOptions.RunContinuationsAsynchronously);
        provisioning.Setup(client => client.ActivateLicenseAsync(It.IsAny<ActivateClientDto>()))
            .Callback(() => entered.TrySetResult()).Returns(response.Task);
        var activation = sut.ActivateLicenseAsync();
        await entered.Task.WaitAsync(TimeSpan.FromSeconds(10));
        try {
            for (var index = 0; index < 100; index++) {
                Assert.Equal(LicenseComplianceStatus.NonCompliant, sut.Snapshot.ComplianceStatus);
                Assert.Null(sut.SessionInfo);
            }
        }
        finally {
            response.TrySetResult(new ApiResponse<LicenseInfoDto> { StatusCode = 200, Result = LicenseSnapshot() });
            await activation;
        }
        Assert.Equal(LicenseComplianceStatus.NonCompliant, sut.Snapshot.ComplianceStatus);
        await sut.AddHeartbeatAsync();
        Assert.Equal(LicenseComplianceStatus.NonCompliant, sut.Snapshot.ComplianceStatus);
        await sut.OpenSessionAsync();
        Assert.Equal(LicenseComplianceStatus.Compliant, sut.Snapshot.ComplianceStatus);
    }

    [Fact]
    public async Task CloseSessionAsync_CanCancelWhileWaitingForAnotherOperation() {
        using var environment = SoftwareVersionScope.Set();
        var (sut, provisioning) = await CreateCompliantSutAsync();
        var response = new TaskCompletionSource<ApiResponse<LicenseInfoDto>>(TaskCreationOptions.RunContinuationsAsynchronously);
        provisioning.Setup(client => client.AddHeartbeatAsync(It.IsAny<AddHeartbeatDto>())).Returns(response.Task);
        var heartbeat = sut.AddHeartbeatAsync();
        using var cancellation = new CancellationTokenSource();
        var closing = sut.CloseSessionAsync(cancellation.Token);
        try {
            Assert.False(closing.IsCompleted);
            cancellation.Cancel();
            await Assert.ThrowsAnyAsync<OperationCanceledException>(() => closing.WaitAsync(TimeSpan.FromSeconds(5)));
            provisioning.Verify(client => client.CloseSessionAsync(It.IsAny<SessionRequestDto>()), Times.Never);
            Assert.False(heartbeat.IsCompleted);
        }
        finally {
            response.TrySetResult(new ApiResponse<LicenseInfoDto> { StatusCode = 200, Result = LicenseSnapshot() });
            await heartbeat;
        }
    }

    [Theory]
    [InlineData("secret")]
    [InlineData("client")]
    [InlineData("provider")]
    [InlineData("retry")]
    public async Task CloseSessionAsync_CanCancelEveryExternalWait(string wait) {
        using var environment = SoftwareVersionScope.Set();
        var secret = new TaskCompletionSource<string>(TaskCreationOptions.RunContinuationsAsynchronously);
        var client = new TaskCompletionSource<ISlasconeClientV2>(TaskCreationOptions.RunContinuationsAsynchronously);
        var response = new TaskCompletionSource<ApiResponse<string>>(TaskCreationOptions.RunContinuationsAsynchronously);
        var secrets = new Mock<ISecretFilesService>();
        secrets.Setup(service => service.GetSecretFromConfigAsync(It.IsAny<string>()))
            .Returns(wait == "secret" ? secret.Task : Task.FromResult(LicenseSnapshot().License_key));
        var provisioning = new Mock<ISlasconeProvisioningClientV2>();
        provisioning.Setup(value => value.CloseSessionAsync(It.IsAny<SessionRequestDto>()))
            .Returns(wait == "retry"
                ? Task.FromResult(new ApiResponse<string> { StatusCode = 500 })
                : response.Task);
        var sdk = new Mock<ISlasconeClientV2>();
        sdk.SetupGet(value => value.Provisioning).Returns(provisioning.Object);
        var sut = CreateSut(new CapturingLogger<LicensingService>(),
            wait == "client" ? client.Task : Task.FromResult(sdk.Object), secrets.Object);
        using var cancellation = new CancellationTokenSource();
        var closing = sut.CloseSessionAsync(cancellation.Token);
        try {
            Assert.False(closing.IsCompleted);
            cancellation.Cancel();
            var error = await Assert.ThrowsAnyAsync<OperationCanceledException>(() => closing.WaitAsync(TimeSpan.FromSeconds(5)));
            Assert.Equal(cancellation.Token, error.CancellationToken);
            Assert.NotEqual(LicenseSessionStatus.Closed, sut.LastOpenSessionAttemptStatus);
        }
        finally {
            secret.TrySetResult(LicenseSnapshot().License_key);
            client.TrySetResult(sdk.Object);
            response.TrySetResult(new ApiResponse<string> { StatusCode = 200, Result = "OK" });
        }
        provisioning.Verify(value => value.CloseSessionAsync(It.IsAny<SessionRequestDto>()),
            wait is "provider" or "retry" ? Times.Once() : Times.Never());
    }

    [Fact]
    public async Task CancelledClose_KeepsSessionNonCompliant_AndSerializesReopenUntilLateSuccess() {
        using var environment = SoftwareVersionScope.Set();
        var (sut, provisioning) = await CreateCompliantSutAsync();
        var response = new TaskCompletionSource<ApiResponse<string>>(TaskCreationOptions.RunContinuationsAsynchronously);
        provisioning.Setup(client => client.CloseSessionAsync(It.IsAny<SessionRequestDto>())).Returns(response.Task);
        provisioning.Invocations.Clear();
        using var cancellation = new CancellationTokenSource();
        var closing = sut.CloseSessionAsync(cancellation.Token);
        Assert.Null(sut.SessionInfo);
        Assert.Equal(LicenseComplianceStatus.NonCompliant, sut.Snapshot.ComplianceStatus);
        cancellation.Cancel();
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => closing.WaitAsync(TimeSpan.FromSeconds(5)));
        var heartbeat = sut.AddHeartbeatAsync();
        var reopening = sut.OpenSessionAsync();
        try {
            Assert.False(heartbeat.IsCompleted);
            Assert.False(reopening.IsCompleted);
            provisioning.Verify(client => client.AddHeartbeatAsync(It.IsAny<AddHeartbeatDto>()), Times.Never);
            provisioning.Verify(client => client.OpenSessionAsync(It.IsAny<SessionRequestDto>()), Times.Never);
            Assert.Equal(LicenseComplianceStatus.NonCompliant, sut.Snapshot.ComplianceStatus);
        }
        finally {
            response.TrySetResult(new ApiResponse<string> { StatusCode = 200, Result = "OK" });
            await Task.WhenAll(heartbeat, reopening).WaitAsync(TimeSpan.FromSeconds(5));
        }
        Assert.Equal(LicenseSessionStatus.Opened, sut.LastOpenSessionAttemptStatus);
        Assert.Equal(LicenseComplianceStatus.Compliant, sut.Snapshot.ComplianceStatus);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task CancelledClose_LateFailureRemainsNonCompliant_AndReleasesSemaphore(bool throws) {
        using var environment = SoftwareVersionScope.Set();
        var (sut, provisioning) = await CreateCompliantSutAsync();
        var response = new TaskCompletionSource<ApiResponse<string>>(TaskCreationOptions.RunContinuationsAsynchronously);
        provisioning.Setup(client => client.CloseSessionAsync(It.IsAny<SessionRequestDto>())).Returns(response.Task);
        using var cancellation = new CancellationTokenSource();
        var closing = sut.CloseSessionAsync(cancellation.Token);
        cancellation.Cancel();
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => closing.WaitAsync(TimeSpan.FromSeconds(5)));
        var heartbeat = sut.AddHeartbeatAsync();
        Assert.False(heartbeat.IsCompleted);
        if (throws) {
            response.SetException(new HttpRequestException(SecretText));
        } else {
            response.SetResult(new ApiResponse<string> {
                StatusCode = 409, Error = new ErrorResultObjects { Id = (int)SlasconeApiErrorCode.UNKNOWN_CLIENT },
            });
        }
        await heartbeat.WaitAsync(TimeSpan.FromSeconds(5));
        Assert.Null(sut.SessionInfo);
        Assert.Equal(LicenseSessionStatus.CloseFailed, sut.LastOpenSessionAttemptStatus);
        Assert.Equal(LicenseComplianceStatus.NonCompliant, sut.Snapshot.ComplianceStatus);
    }

    [Fact]
    public async Task CloseAndHeartbeat_AreSerialized_AndHeartbeatCannotRestoreClosedSession() {
        using var environment = SoftwareVersionScope.Set();
        var (sut, provisioning) = await CreateCompliantSutAsync();
        var entered = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var response = new TaskCompletionSource<ApiResponse<string>>(TaskCreationOptions.RunContinuationsAsynchronously);
        provisioning.Setup(client => client.CloseSessionAsync(It.IsAny<SessionRequestDto>()))
            .Callback(() => entered.TrySetResult()).Returns(response.Task);
        provisioning.Invocations.Clear();
        var closing = sut.CloseSessionAsync();
        await entered.Task.WaitAsync(TimeSpan.FromSeconds(10));
        var heartbeat = sut.AddHeartbeatAsync();
        try {
            Assert.False(heartbeat.IsCompleted);
            provisioning.Verify(client => client.AddHeartbeatAsync(It.IsAny<AddHeartbeatDto>()), Times.Never);
        }
        finally {
            response.TrySetResult(new ApiResponse<string> { StatusCode = 200, Result = "OK" });
            await Task.WhenAll(closing, heartbeat);
        }
        Assert.Null(sut.SessionInfo);
        Assert.Equal(LicenseComplianceStatus.NonCompliant, sut.Snapshot.ComplianceStatus);
    }

    [Fact]
    public async Task UnknownClientHeartbeat_ClearsSessionAndRequestsReactivation() {
        using var environment = SoftwareVersionScope.Set();
        var (sut, provisioning) = await CreateCompliantSutAsync();
        provisioning.Setup(client => client.AddHeartbeatAsync(It.IsAny<AddHeartbeatDto>()))
            .ReturnsAsync(new ApiResponse<LicenseInfoDto> {
                StatusCode = 400, Error = new ErrorResultObjects { Id = (int)SlasconeApiErrorCode.UNKNOWN_CLIENT },
            });

        await sut.AddHeartbeatAsync();

        Assert.Null(sut.LicenseInfo);
        Assert.Null(sut.SessionInfo);
        Assert.Equal(LicenseActivationStatus.Unassigned, sut.ActivationStatus);
        Assert.Equal(LicenseComplianceStatus.NonCompliant, sut.Snapshot.ComplianceStatus);
    }

    private static async Task<(LicensingService Sut, Mock<ISlasconeProvisioningClientV2> Provisioning)> CreateCompliantSutAsync() {
        var (sut, provisioning) = CreateProvisioningSut();
        provisioning.Setup(client => client.AddHeartbeatAsync(It.IsAny<AddHeartbeatDto>()))
            .ReturnsAsync(new ApiResponse<LicenseInfoDto> { StatusCode = 200, Result = LicenseSnapshot() });
        provisioning.Setup(client => client.OpenSessionAsync(It.IsAny<SessionRequestDto>()))
            .ReturnsAsync(new ApiResponse<SessionStatusDto> { StatusCode = 200, Result = new SessionStatusDto {
                Is_session_valid = true, Session_created_date = DateTimeOffset.UtcNow, Session_valid_until = DateTimeOffset.UtcNow.AddHours(1),
            } });
        await sut.AddHeartbeatAsync();
        await sut.OpenSessionAsync();
        Assert.Equal(LicenseComplianceStatus.Compliant, sut.Snapshot.ComplianceStatus);
        return (sut, provisioning);
    }

    [Theory]
    [InlineData(true, 60, true)]
    [InlineData(false, 60, false)]
    [InlineData(true, -60, false)]
    [InlineData(false, -60, false)]
    public async Task Compliance_RequiresBothSessionValidityAndFutureExpiration(bool isValid, int expirationMinutes, bool isCompliant) {
        using var environment = SoftwareVersionScope.Set();
        var (sut, provisioning) = CreateProvisioningSut();
        provisioning.Setup(client => client.AddHeartbeatAsync(It.IsAny<AddHeartbeatDto>()))
            .ReturnsAsync(new ApiResponse<LicenseInfoDto> { StatusCode = 200, Result = LicenseSnapshot() });
        provisioning.Setup(client => client.OpenSessionAsync(It.IsAny<SessionRequestDto>()))
            .ReturnsAsync(new ApiResponse<SessionStatusDto> {
                StatusCode = 200,
                Result = new SessionStatusDto {
                    Is_session_valid = isValid,
                    Session_valid_until = DateTimeOffset.UtcNow.AddMinutes(expirationMinutes),
                },
            });

        await sut.AddHeartbeatAsync();
        Assert.Equal(LicenseComplianceStatus.NonCompliant, sut.ComplianceStatus);
        await sut.OpenSessionAsync();

        Assert.Equal(isCompliant ? LicenseComplianceStatus.Compliant : LicenseComplianceStatus.NonCompliant, sut.ComplianceStatus);
    }

    private static (LicensingService Sut, Mock<ISlasconeProvisioningClientV2> Provisioning) CreateProvisioningSut(ISecretFilesService? secretFiles = null) {
        var provisioning = new Mock<ISlasconeProvisioningClientV2>();
        var client = new Mock<ISlasconeClientV2>();
        client.SetupGet(value => value.Provisioning).Returns(provisioning.Object);
        return (CreateSut(new CapturingLogger<LicensingService>(), Task.FromResult(client.Object), secretFiles), provisioning);
    }

    [Fact]
    public async Task HeartbeatForNewConfiguredLicense_DoesNotReusePreviousLicenseSession() {
        using var environment = SoftwareVersionScope.Set();
        var configuredKey = LicenseSnapshot().License_key;
        var secrets = new Mock<ISecretFilesService>();
        secrets.Setup(service => service.GetSecretFromConfigAsync(It.IsAny<string>())).ReturnsAsync(() => configuredKey);
        var (sut, provisioning) = CreateProvisioningSut(secrets.Object);
        provisioning.Setup(client => client.AddHeartbeatAsync(It.IsAny<AddHeartbeatDto>()))
            .ReturnsAsync(() => new ApiResponse<LicenseInfoDto> { StatusCode = 200, Result = new LicenseInfoDto {
                License_key = configuredKey, Is_license_valid = true,
            } });
        provisioning.Setup(client => client.OpenSessionAsync(It.IsAny<SessionRequestDto>()))
            .ReturnsAsync(new ApiResponse<SessionStatusDto> { StatusCode = 200, Result = new SessionStatusDto {
                Is_session_valid = true, Session_valid_until = DateTimeOffset.UtcNow.AddHours(1),
            } });
        await sut.AddHeartbeatAsync();
        await sut.OpenSessionAsync();
        Assert.Equal(LicenseComplianceStatus.Compliant, sut.Snapshot.ComplianceStatus);

        configuredKey = Guid.NewGuid().ToString();
        await sut.AddHeartbeatAsync();

        Assert.Equal(configuredKey, sut.Snapshot.LicenseInfo!.License_key);
        Assert.Null(sut.SessionInfo);
        Assert.Equal(LicenseComplianceStatus.NonCompliant, sut.Snapshot.ComplianceStatus);
        await sut.OpenSessionAsync();
        Assert.Equal(LicenseComplianceStatus.Compliant, sut.Snapshot.ComplianceStatus);
    }

    [Fact]
    public async Task LicenseChangeDuringSessionRequest_DiscardsResponseAndRequiresReactivation() {
        using var environment = SoftwareVersionScope.Set();
        var configuredKey = LicenseSnapshot().License_key;
        var secrets = new Mock<ISecretFilesService>();
        secrets.Setup(service => service.GetSecretFromConfigAsync(It.IsAny<string>())).ReturnsAsync(() => configuredKey);
        var (sut, provisioning) = CreateProvisioningSut(secrets.Object);
        provisioning.Setup(client => client.AddHeartbeatAsync(It.IsAny<AddHeartbeatDto>()))
            .ReturnsAsync(new ApiResponse<LicenseInfoDto> { StatusCode = 200, Result = LicenseSnapshot() });
        provisioning.Setup(client => client.OpenSessionAsync(It.IsAny<SessionRequestDto>()))
            .Callback(() => configuredKey = Guid.NewGuid().ToString())
            .ReturnsAsync(new ApiResponse<SessionStatusDto> { StatusCode = 200, Result = new SessionStatusDto {
                Is_session_valid = true, Session_valid_until = DateTimeOffset.UtcNow.AddHours(1),
            } });
        await sut.AddHeartbeatAsync();

        await sut.OpenSessionAsync();

        Assert.Null(sut.Snapshot.LicenseInfo);
        Assert.Null(sut.SessionInfo);
        Assert.Equal(LicenseActivationStatus.ActivationFailed, sut.ActivationStatus);
        Assert.Equal(LicenseComplianceStatus.NonCompliant, sut.Snapshot.ComplianceStatus);
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
