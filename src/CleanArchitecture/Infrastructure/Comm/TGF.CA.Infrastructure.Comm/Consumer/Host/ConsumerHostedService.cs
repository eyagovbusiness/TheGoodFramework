using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using TGF.CA.Infrastructure.Comm.Consumer.Manager;

namespace TGF.CA.Infrastructure.Comm.Consumer.Host;

/// <summary>
/// Runs a message consumer for the lifetime of the host, restarting it whenever the broker connection drops.
/// </summary>
/// <remarks>
/// Consumption is retried indefinitely with a capped exponential backoff. A bounded retry budget used to make
/// a routine broker restart terminal: once the budget was exhausted the execution task faulted, nothing ever
/// restarted the consumer, and the service stayed unready for the rest of the process lifetime.
/// </remarks>
public class ConsumerHostedService<TMessage> : IHostedService {
    private readonly IConsumerManager<TMessage> _consumerManager;
    private readonly IMessageConsumer<TMessage> _messageConsumer;
    private readonly ILogger<ConsumerHostedService<TMessage>> _logger;
    private readonly TimeSpan _initialRetryDelay;
    private readonly TimeSpan _maxRetryDelay;
    private readonly CancellationTokenSource _stoppingCancellationTokenSource = new();
    private Task? _executingTask;

    internal static TimeSpan DefaultInitialRetryDelay { get; } = TimeSpan.FromSeconds(2);
    internal static TimeSpan DefaultMaxRetryDelay { get; } = TimeSpan.FromSeconds(30);

    public ConsumerHostedService(IConsumerManager<TMessage> consumerManager, IMessageConsumer<TMessage> messageConsumer, ILogger<ConsumerHostedService<TMessage>> logger)
        : this(consumerManager, messageConsumer, logger, DefaultInitialRetryDelay, DefaultMaxRetryDelay) { }

    /// <summary>
    /// Test seam: lets a test shorten the backoff so the restart behaviour can be verified quickly.
    /// </summary>
    internal ConsumerHostedService(IConsumerManager<TMessage> consumerManager, IMessageConsumer<TMessage> messageConsumer, ILogger<ConsumerHostedService<TMessage>> logger, TimeSpan initialRetryDelay, TimeSpan maxRetryDelay) {
        _consumerManager = consumerManager;
        _messageConsumer = messageConsumer;
        _logger = logger;
        _initialRetryDelay = initialRetryDelay;
        _maxRetryDelay = maxRetryDelay;
    }

    public Task StartAsync(CancellationToken cancellationToken) {
        _executingTask = ExecuteAsync(_stoppingCancellationTokenSource.Token);
        return _executingTask.IsCompleted ? _executingTask : Task.CompletedTask;
    }

    public Task StopAsync(CancellationToken cancellationToken) {
        _stoppingCancellationTokenSource.Cancel();
        _consumerManager.StopExecution();
        return Task.CompletedTask;
    }

    // Every outcome is observed here so a failure can never become an unobserved faulted task.
    internal async Task ExecuteAsync(CancellationToken stoppingToken) {
        var delay = _initialRetryDelay;
        while (!stoppingToken.IsCancellationRequested) {
            try {
                await ConsumeMessages(stoppingToken);
                return; // Cancelled or asked to stop: nothing left to do.
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested) {
                return; // The host is stopping.
            }
            catch (Exception) {
                // Fixed text only: broker failures can carry connection details.
                _logger.LogWarning("The message consumer stopped after a failure and will be restarted.");
            }

            try {
                await Task.Delay(delay, stoppingToken);
            }
            catch (OperationCanceledException) {
                return; // The host is stopping.
            }

            delay = NextDelay(delay, _maxRetryDelay);
        }
    }

    internal static TimeSpan NextDelay(TimeSpan current, TimeSpan max) {
        var next = TimeSpan.FromTicks(current.Ticks * 2);
        return next > max ? max : next;
    }

    private async Task ConsumeMessages(CancellationToken cancellationToken) {
        while (!cancellationToken.IsCancellationRequested) {
            var ct = _consumerManager.GetCancellationToken();
            if (ct.IsCancellationRequested) break;
            try {
                await _messageConsumer.StartAsync(cancellationToken);
            }
            catch (OperationCanceledException) {
                // ignore, the operation is getting cancelled
            }
        }
    }
}
