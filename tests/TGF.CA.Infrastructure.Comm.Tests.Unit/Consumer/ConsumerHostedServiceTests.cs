using Microsoft.Extensions.Logging.Abstractions;
using TGF.CA.Infrastructure.Comm.Consumer;
using TGF.CA.Infrastructure.Comm.Consumer.Host;
using TGF.CA.Infrastructure.Comm.Consumer.Manager;
using TGF.CA.Infrastructure.Comm.Messages;

namespace TGF.CA.Infrastructure.Comm.Tests.Unit.Consumer;

/// <summary>
/// Regression coverage for SF-2975: consumption used to be wrapped in a bounded 10 attempt retry budget, so a
/// routine broker restart permanently killed the consumer once the budget was exhausted.
/// </summary>
public class ConsumerHostedServiceTests {
    private static readonly TimeSpan Fast = TimeSpan.FromMilliseconds(5);

    [Fact]
    public async Task ExecuteAsync_KeepsRestartingTheConsumer_BeyondTheOldTenAttemptBudget() {
        var consumer = new CountingConsumer(failures: int.MaxValue);
        var sut = CreateSut(consumer);
        using var cancellation = new CancellationTokenSource();

        var executing = sut.ExecuteAsync(cancellation.Token);
        await consumer.WaitForAttempts(15, TimeSpan.FromSeconds(10));
        cancellation.Cancel();
        await executing;

        // The old implementation stopped forever at 10.
        Assert.True(consumer.Attempts >= 15, $"expected the consumer to keep restarting, saw {consumer.Attempts} attempts");
    }

    [Fact]
    public async Task ExecuteAsync_StopsPromptly_WhenCancelled() {
        var consumer = new CountingConsumer(failures: int.MaxValue);
        var sut = CreateSut(consumer);
        using var cancellation = new CancellationTokenSource();

        var executing = sut.ExecuteAsync(cancellation.Token);
        await consumer.WaitForAttempts(1, TimeSpan.FromSeconds(5));
        cancellation.Cancel();

        await executing.WaitAsync(TimeSpan.FromSeconds(5));
        Assert.True(executing.IsCompletedSuccessfully);
    }

    [Fact]
    public void NextDelay_GrowsExponentially_AndIsCapped() {
        var max = TimeSpan.FromSeconds(30);

        Assert.Equal(TimeSpan.FromSeconds(4), ConsumerHostedService<IntegrationMessage>.NextDelay(TimeSpan.FromSeconds(2), max));
        Assert.Equal(TimeSpan.FromSeconds(8), ConsumerHostedService<IntegrationMessage>.NextDelay(TimeSpan.FromSeconds(4), max));
        Assert.Equal(max, ConsumerHostedService<IntegrationMessage>.NextDelay(TimeSpan.FromSeconds(20), max));
        Assert.Equal(max, ConsumerHostedService<IntegrationMessage>.NextDelay(max, max));
    }

    private static ConsumerHostedService<IntegrationMessage> CreateSut(IMessageConsumer<IntegrationMessage> consumer)
    => new(
        new ConsumerManager<IntegrationMessage>(),
        consumer,
        NullLogger<ConsumerHostedService<IntegrationMessage>>.Instance,
        Fast,
        Fast);

    private sealed class CountingConsumer(int failures) : IMessageConsumer<IntegrationMessage> {
        private int _attempts;

        public int Attempts => Volatile.Read(ref _attempts);

        public Task StartAsync(CancellationToken aCancellationToken = default) {
            var attempt = Interlocked.Increment(ref _attempts);
            aCancellationToken.ThrowIfCancellationRequested();
            if (attempt <= failures)
                throw new InvalidOperationException("broker connection lost");
            return Task.CompletedTask;
        }

        public async Task WaitForAttempts(int target, TimeSpan timeout) {
            var deadline = DateTime.UtcNow + timeout;
            while (Attempts < target && DateTime.UtcNow < deadline)
                await Task.Delay(10);
        }
    }
}
