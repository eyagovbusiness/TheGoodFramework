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
using TGF.CA.Infrastructure.Licensing.Tests.Unit.TestSupport;

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
    public async Task StopAsync_PassesShutdownBudgetToClose_WhenMaintenanceOutlastsIt() {
        var licensing = new Mock<ILicensingService>();
        licensing.SetupGet(service => service.HeartbeatStatus).Returns(LicenseHeartbeatStatus.Success);
        licensing.SetupGet(service => service.ActivationStatus).Returns(LicenseActivationStatus.Activated);
        var entered = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var response = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var calls = 0;
        licensing.Setup(service => service.AddHeartbeatAsync()).Returns(() => {
            if (++calls == 1) return Task.CompletedTask;
            entered.TrySetResult();
            return response.Task;
        });
        using var cancellation = new CancellationTokenSource();
        licensing.Setup(service => service.CloseSessionAsync(cancellation.Token))
            .Returns((CancellationToken token) => Task.Delay(Timeout.InfiniteTimeSpan, token));
        using var hosted = CreateHosted(licensing.Object);
        await hosted.StartAsync(CancellationToken.None);
        try {
            await entered.Task.WaitAsync(TimeSpan.FromSeconds(5));
            cancellation.CancelAfter(TimeSpan.FromMilliseconds(50));
            var error = await Assert.ThrowsAnyAsync<OperationCanceledException>(() => hosted.StopAsync(cancellation.Token).WaitAsync(TimeSpan.FromSeconds(5)));
            Assert.Equal(cancellation.Token, error.CancellationToken);
            Assert.Equal("Closing the license session was cancelled.", error.Message);
            Assert.Null(error.InnerException);
            licensing.Verify(service => service.CloseSessionAsync(cancellation.Token), Times.Once);
        }
        finally {
            response.TrySetResult();
            await hosted.ExecuteTask!.WaitAsync(TimeSpan.FromSeconds(5));
        }
    }

    [Theory]
    [InlineData(0, 100, 2)]
    [InlineData(1, 100, 4)]
    [InlineData(4, 100, 30)]
    [InlineData(31, 100, 30)]
    [InlineData(4, 3, 3)]
    [InlineData(4, 0, 0)]
    [InlineData(4, -1, 0)]
    public void RenewalRetryDelay_GrowsToMaximumButNeverPastExpiration(int attempt, int remainingSeconds, int expectedSeconds) {
        var now = DateTimeOffset.UtcNow;
        var session = new SessionStatusDto { Session_valid_until = now.AddSeconds(remainingSeconds) };
        var delay = LicensingStartupHostedService.CalculateRenewalRetryDelay(session, now, attempt, new StartupOptions());
        Assert.Equal(TimeSpan.FromSeconds(expectedSeconds), delay);
    }

    [Fact]
    public async Task SuccessfulRenewal_DoesNotApplyRetryBackoffOrExceedHealthyRefreshInterval() {
        var licensing = new Mock<ILicensingService>();
        var now = DateTimeOffset.UtcNow;
        var session = new SessionStatusDto {
            Is_session_valid = true, Session_created_date = now.AddMinutes(-10), Session_valid_until = now.AddSeconds(1),
        };
        licensing.SetupGet(service => service.ActivationStatus).Returns(LicenseActivationStatus.Activated);
        licensing.SetupGet(service => service.HeartbeatStatus).Returns(LicenseHeartbeatStatus.Success);
        licensing.SetupGet(service => service.LicenseInfo).Returns(new LicenseInfoDto { Is_license_valid = true });
        licensing.SetupGet(service => service.SessionInfo).Returns(() => session);
        var refreshed = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var renewed = false;
        licensing.Setup(service => service.AddHeartbeatAsync()).Callback(() => {
            if (renewed) refreshed.TrySetResult();
        }).Returns(Task.CompletedTask);
        licensing.Setup(service => service.OpenSessionAsync()).Callback(() => {
            var created = DateTimeOffset.UtcNow;
            session = new SessionStatusDto {
                Is_session_valid = true, Session_created_date = created, Session_valid_until = created.AddSeconds(5),
            };
            renewed = true;
        }).Returns(Task.CompletedTask);
        var logger = new CapturingLogger<LicensingStartupHostedService>();
        using var hosted = new LicensingStartupHostedService(licensing.Object, Options.Create(new SlasconeOptions {
            ApiBaseUrl = "https://slascone.invalid", IsvId = Guid.Empty, ProductId = Guid.Empty,
            StartupOptions = new StartupOptions { LicenseRefreshIntervalSeconds = 1, InitialBackoffSeconds = 30 },
        }), logger);
        await hosted.StartAsync(CancellationToken.None);
        try {
            await refreshed.Task.WaitAsync(TimeSpan.FromSeconds(3));
        }
        finally {
            using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(5));
            await hosted.StopAsync(timeout.Token);
        }
        Assert.DoesNotContain(logger.Entries, entry => entry.Message.Contains("Session renewal is still due"));
    }

    [Theory]
    [InlineData(LicenseSessionStatus.Opened, false)]
    [InlineData(LicenseSessionStatus.OpenFailed, false)]
    [InlineData(LicenseSessionStatus.OpenFailed, true)]
    public async Task NonExtendingRenewals_BackOffExponentially_AndResetOnlyWhenExpirationAdvances(LicenseSessionStatus status, bool throws) {
        var licensing = new Mock<ILicensingService>();
        var now = DateTimeOffset.UtcNow;
        var session = new SessionStatusDto {
            Is_session_valid = true, Session_created_date = now.AddSeconds(-1000), Session_valid_until = now.AddSeconds(100),
        };
        licensing.SetupGet(service => service.ActivationStatus).Returns(LicenseActivationStatus.Activated);
        licensing.SetupGet(service => service.HeartbeatStatus).Returns(LicenseHeartbeatStatus.Success);
        licensing.SetupGet(service => service.LicenseInfo).Returns(new LicenseInfoDto { Is_license_valid = true });
        licensing.SetupGet(service => service.SessionInfo).Returns(() => session);
        licensing.SetupGet(service => service.LastOpenSessionAttemptStatus).Returns(status);
        var completed = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var calls = 0;
        licensing.Setup(service => service.OpenSessionAsync()).Callback(() => {
            if (++calls == 3) session.Session_valid_until = now.AddSeconds(200);
            if (calls == 4) {
                session = new SessionStatusDto {
                    Is_session_valid = true, Session_created_date = now, Session_valid_until = now.AddHours(1),
                };
                completed.TrySetResult();
            }
        }).Returns(() => throws && calls <= 2 ? Task.FromException(new HttpRequestException("Renewal failed")) : Task.CompletedTask);
        var logger = new CapturingLogger<LicensingStartupHostedService>();
        using var hosted = new LicensingStartupHostedService(licensing.Object, Options.Create(new SlasconeOptions {
            ApiBaseUrl = "https://slascone.invalid", IsvId = Guid.Empty, ProductId = Guid.Empty,
            StartupOptions = new StartupOptions { InitialBackoffSeconds = 1, MaxBackoffSeconds = 4 },
        }), logger);
        await hosted.StartAsync(CancellationToken.None);
        try {
            await completed.Task.WaitAsync(TimeSpan.FromSeconds(10));
        }
        finally {
            using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(5));
            await hosted.StopAsync(timeout.Token);
        }
        var delays = logger.Entries.Where(entry => entry.Message.Contains("Session renewal is still due"))
            .SelectMany(entry => entry.StructuredState.Where(value => value.StartsWith("Delay="))).ToArray();
        Assert.Equal(new[] { "Delay=00:00:01", "Delay=00:00:02", "Delay=00:00:01" }, delays);
    }

    [Fact]
    public async Task NonExtendingRenewals_WithoutCreationDate_UseIncreasingRetriesInsteadOfMovingRenewalTarget() {
        var licensing = new Mock<ILicensingService>();
        var now = DateTimeOffset.UtcNow;
        var session = new SessionStatusDto {
            Is_session_valid = true, Session_created_date = now.AddSeconds(-1000), Session_valid_until = now.AddSeconds(100),
        };
        licensing.SetupGet(service => service.ActivationStatus).Returns(LicenseActivationStatus.Activated);
        licensing.SetupGet(service => service.HeartbeatStatus).Returns(LicenseHeartbeatStatus.Success);
        licensing.SetupGet(service => service.LicenseInfo).Returns(new LicenseInfoDto { Is_license_valid = true });
        licensing.SetupGet(service => service.SessionInfo).Returns(() => session);
        var completed = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var calls = 0;
        licensing.Setup(service => service.OpenSessionAsync()).Callback(() => {
            session.Session_created_date = null;
            if (++calls == 3) {
                session = new SessionStatusDto {
                    Is_session_valid = true, Session_created_date = now, Session_valid_until = now.AddHours(1),
                };
                completed.TrySetResult();
            }
        }).Returns(Task.CompletedTask);
        var logger = new CapturingLogger<LicensingStartupHostedService>();
        using var hosted = new LicensingStartupHostedService(licensing.Object, Options.Create(new SlasconeOptions {
            ApiBaseUrl = "https://slascone.invalid", IsvId = Guid.Empty, ProductId = Guid.Empty,
            StartupOptions = new StartupOptions { InitialBackoffSeconds = 1, MaxBackoffSeconds = 4 },
        }), logger);
        await hosted.StartAsync(CancellationToken.None);
        try {
            await completed.Task.WaitAsync(TimeSpan.FromSeconds(10));
        }
        finally {
            using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(5));
            await hosted.StopAsync(timeout.Token);
        }
        var delays = logger.Entries.Where(entry => entry.Message.Contains("Session renewal is still due"))
            .SelectMany(entry => entry.StructuredState.Where(value => value.StartsWith("Delay="))).ToArray();
        Assert.Equal(new[] { "Delay=00:00:01", "Delay=00:00:02" }, delays);
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
        licensing.Setup(service => service.CloseSessionAsync(It.IsAny<CancellationToken>())).Returns(Task.CompletedTask);
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
        licensing.Setup(service => service.CloseSessionAsync(It.IsAny<CancellationToken>())).Returns(Task.CompletedTask);
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
        licensing.Setup(service => service.CloseSessionAsync(It.IsAny<CancellationToken>())).Callback(() => Assert.True(hosted.ExecuteTask!.IsCompleted)).Returns(Task.CompletedTask);
        await hosted.StartAsync(CancellationToken.None);
        try {
            await maintaining.Task.WaitAsync(TimeSpan.FromSeconds(10));
        }
        finally {
            using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(10));
            await hosted.StopAsync(timeout.Token);
        }
        Assert.True(hosted.ExecuteTask!.IsCompleted);
        licensing.Verify(service => service.CloseSessionAsync(It.IsAny<CancellationToken>()), Times.Once);
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
        licensing.Setup(service => service.CloseSessionAsync(It.IsAny<CancellationToken>())).Returns(Task.CompletedTask);
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
