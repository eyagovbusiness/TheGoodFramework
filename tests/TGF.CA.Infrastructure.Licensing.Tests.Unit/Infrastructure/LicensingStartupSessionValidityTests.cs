using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Microsoft.AspNetCore.Builder;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Moq;
using Slascone.Client;
using TGF.CA.Infrastructure.Licensing;
using TGF.CA.Infrastructure.Licensing.Slascone;
using TGF.CA.Infrastructure.Licensing.Slascone.Contracts;
using TGF.CA.Infrastructure.Licensing.Slascone.Services;

namespace TGF.CA.Infrastructure.Licensing.Tests.Unit.Infrastructure;

public class LicensingStartupSessionValidityTests {
    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    public void Registration_RejectsNonPositiveRefreshInterval(int seconds) {
        var builder = WebApplication.CreateBuilder();
        builder.Configuration.AddInMemoryCollection(new Dictionary<string, string?> {
            ["Licensing:Slascone:StartupOptions:LicenseRefreshIntervalSeconds"] = seconds.ToString(),
        });
        builder.ConfigureSlasconeLicensing();
        using var services = builder.Services.BuildServiceProvider();
        var error = Assert.Throws<OptionsValidationException>(() => services.GetRequiredService<IOptions<SlasconeOptions>>().Value);
        Assert.Contains("LicenseRefreshIntervalSeconds", error.Message);
    }

    [Theory]
    [InlineData(86400, 0, 3600)]
    [InlineData(3600, 0, 2880)]
    [InlineData(100, 0, 80)]
    [InlineData(100, 79, 1)]
    [InlineData(100, 80, 0)]
    [InlineData(100, 101, 0)]
    public void RenewalTiming_IsBoundedByRefreshIntervalAndNeverDelaysAnEarlierRenewal(int durationSeconds, int elapsedSeconds, int expectedDelaySeconds) {
        var created = new DateTimeOffset(2026, 11, 2, 12, 0, 0, TimeSpan.Zero);
        var session = new SessionStatusDto { Session_created_date = created, Session_valid_until = created.AddSeconds(durationSeconds) };
        var refreshSeconds = new StartupOptions().LicenseRefreshIntervalSeconds;
        Assert.Equal(3600, refreshSeconds);
        var (_, delay) = LicensingStartupHostedService.CalculateRenewalTiming(session, created.AddSeconds(elapsedSeconds), TimeSpan.FromSeconds(refreshSeconds));
        Assert.Equal(TimeSpan.FromSeconds(expectedDelaySeconds), delay);
    }

    [Fact]
    public async Task Maintenance_ReactivatesAfterAssignmentIsLost_WithoutRestart() {
        var licensing = new Mock<ILicensingService>();
        var heartbeat = LicenseHeartbeatStatus.Success;
        var activation = LicenseActivationStatus.Activated;
        LicenseInfoDto? license = new() { Is_license_valid = true };
        var calls = 0;
        var opened = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        licensing.SetupGet(service => service.HeartbeatStatus).Returns(() => heartbeat);
        licensing.SetupGet(service => service.ActivationStatus).Returns(() => activation);
        licensing.SetupGet(service => service.LicenseInfo).Returns(() => license);
        licensing.Setup(service => service.AddHeartbeatAsync()).Callback(() => {
            if (++calls is 2 or 3) {
                heartbeat = LicenseHeartbeatStatus.Failed;
                activation = LicenseActivationStatus.ActivationFailed;
                license = null;
            } else {
                heartbeat = LicenseHeartbeatStatus.Success;
            }
        }).Returns(Task.CompletedTask);
        licensing.Setup(service => service.ActivateLicenseAsync()).Callback(() => {
            activation = LicenseActivationStatus.Activated;
            heartbeat = LicenseHeartbeatStatus.None;
            license = new LicenseInfoDto { Is_license_valid = true };
        }).Returns(Task.CompletedTask);
        licensing.Setup(service => service.OpenSessionAsync()).Callback(() => opened.TrySetResult()).Returns(Task.CompletedTask);
        licensing.Setup(service => service.CloseSessionAsync()).Returns(Task.CompletedTask);
        licensing.SetupGet(service => service.LastOpenSessionAttemptStatus).Returns(LicenseSessionStatus.Closed);
        using var hosted = CreateHosted(licensing.Object);
        await hosted.StartAsync(CancellationToken.None);
        try {
            await opened.Task.WaitAsync(TimeSpan.FromSeconds(10));
            licensing.Verify(service => service.ActivateLicenseAsync(), Times.Once);
            Assert.Equal(LicenseHeartbeatStatus.Success, heartbeat);
        }
        finally {
            using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(10));
            await hosted.StopAsync(timeout.Token);
        }
    }

    [Fact]
    public async Task HealthyLongSession_RefreshesLicenseBeforeSessionRenewal() {
        var licensing = new Mock<ILicensingService>();
        licensing.SetupGet(service => service.ActivationStatus).Returns(LicenseActivationStatus.Activated);
        licensing.SetupGet(service => service.HeartbeatStatus).Returns(LicenseHeartbeatStatus.Success);
        licensing.SetupGet(service => service.LicenseInfo).Returns(new LicenseInfoDto { Is_license_valid = true });
        licensing.SetupGet(service => service.SessionInfo).Returns(new SessionStatusDto {
            Is_session_valid = true, Session_created_date = DateTimeOffset.UtcNow, Session_valid_until = DateTimeOffset.UtcNow.AddHours(1),
        });
        var refreshed = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var calls = 0;
        licensing.Setup(service => service.AddHeartbeatAsync()).Callback(() => {
            if (Interlocked.Increment(ref calls) >= 3) refreshed.TrySetResult();
        }).Returns(Task.CompletedTask);
        licensing.Setup(service => service.CloseSessionAsync()).Returns(Task.CompletedTask);
        licensing.SetupGet(service => service.LastOpenSessionAttemptStatus).Returns(LicenseSessionStatus.Closed);
        using var hosted = CreateHosted(licensing.Object, refreshSeconds: 1);
        await hosted.StartAsync(CancellationToken.None);
        try {
            await refreshed.Task.WaitAsync(TimeSpan.FromSeconds(10));
            licensing.Verify(service => service.OpenSessionAsync(), Times.Never);
        }
        finally {
            using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(10));
            await hosted.StopAsync(timeout.Token);
        }
    }

    [Fact]
    public async Task StopWithoutActivationGate_CancelsMaintenanceBeforeClosingSession() {
        var licensing = new Mock<ILicensingService>();
        licensing.SetupGet(service => service.ActivationStatus).Returns(LicenseActivationStatus.Activated);
        licensing.SetupGet(service => service.HeartbeatStatus).Returns(LicenseHeartbeatStatus.Success);
        licensing.SetupGet(service => service.LicenseInfo).Returns(new LicenseInfoDto { Is_license_valid = true });
        licensing.SetupGet(service => service.SessionInfo).Returns(new SessionStatusDto {
            Is_session_valid = true, Session_created_date = DateTimeOffset.UtcNow, Session_valid_until = DateTimeOffset.UtcNow.AddHours(1),
        });
        var maintaining = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var calls = 0;
        licensing.Setup(service => service.AddHeartbeatAsync()).Callback(() => {
            if (Interlocked.Increment(ref calls) >= 2) maintaining.TrySetResult();
        }).Returns(Task.CompletedTask);
        licensing.SetupGet(service => service.LastOpenSessionAttemptStatus).Returns(LicenseSessionStatus.Closed);
        using var hosted = new LicensingStartupHostedService(licensing.Object, Options.Create(new SlasconeOptions {
            ApiBaseUrl = "https://slascone.invalid", IsvId = Guid.Empty, ProductId = Guid.Empty,
        }), NullLogger<LicensingStartupHostedService>.Instance);
        licensing.Setup(service => service.CloseSessionAsync()).Callback(() => Assert.True(hosted.ExecuteTask!.IsCompleted)).Returns(Task.CompletedTask);
        await hosted.StartAsync(CancellationToken.None);
        try {
            await maintaining.Task.WaitAsync(TimeSpan.FromSeconds(10));
        }
        finally {
            using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(10));
            await hosted.StopAsync(timeout.Token);
        }
        Assert.True(hosted.ExecuteTask!.IsCompleted);
        licensing.Verify(service => service.CloseSessionAsync(), Times.Once);
    }

    private static LicensingStartupHostedService CreateHosted(ILicensingService licensing, int refreshSeconds = 3600)
        => new(licensing, Options.Create(new SlasconeOptions {
            ApiBaseUrl = "https://slascone.invalid", IsvId = Guid.Empty, ProductId = Guid.Empty,
            StartupOptions = new StartupOptions { LicenseRefreshIntervalSeconds = refreshSeconds },
        }), NullLogger<LicensingStartupHostedService>.Instance, _ => Task.CompletedTask);

    [Theory]
    [InlineData(true, -60)]
    [InlineData(false, 60)]
    public async Task Maintenance_ReopensExpiredOrProviderInvalidSession(bool isValid, int expirationMinutes) {
        var licensing = new Mock<ILicensingService>();
        licensing.SetupGet(service => service.ActivationStatus).Returns(LicenseActivationStatus.Activated);
        licensing.SetupGet(service => service.HeartbeatStatus).Returns(LicenseHeartbeatStatus.Success);
        licensing.SetupGet(service => service.LicenseInfo).Returns(new LicenseInfoDto { Is_license_valid = true });
        licensing.SetupGet(service => service.SessionInfo).Returns(new SessionStatusDto {
            Is_session_valid = isValid,
            Session_valid_until = DateTimeOffset.UtcNow.AddMinutes(expirationMinutes),
        });
        var opened = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        licensing.Setup(service => service.AddHeartbeatAsync()).Returns(Task.CompletedTask);
        licensing.Setup(service => service.OpenSessionAsync()).Callback(() => {
            licensing.SetupGet(service => service.SessionInfo).Returns(new SessionStatusDto {
                Is_session_valid = true,
                Session_valid_until = DateTimeOffset.UtcNow.AddHours(1),
            });
            opened.TrySetResult();
        }).Returns(Task.CompletedTask);
        licensing.Setup(service => service.CloseSessionAsync()).Returns(Task.CompletedTask);
        licensing.SetupGet(service => service.LastOpenSessionAttemptStatus).Returns(LicenseSessionStatus.Closed);
        using var hosted = new LicensingStartupHostedService(
            licensing.Object, Options.Create(new SlasconeOptions {
                ApiBaseUrl = "https://slascone.invalid", IsvId = Guid.Empty, ProductId = Guid.Empty,
            }),
            NullLogger<LicensingStartupHostedService>.Instance,
            _ => Task.CompletedTask);

        await hosted.StartAsync(CancellationToken.None);
        try {
            await opened.Task.WaitAsync(TimeSpan.FromSeconds(10));
            licensing.Verify(service => service.OpenSessionAsync(), Times.Once);
        }
        finally {
            using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(10));
            await hosted.StopAsync(timeout.Token);
        }
    }
}
