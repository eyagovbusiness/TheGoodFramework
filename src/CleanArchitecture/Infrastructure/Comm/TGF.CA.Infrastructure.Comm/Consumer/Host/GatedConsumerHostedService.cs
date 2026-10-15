using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using TGF.CA.Infrastructure.Comm.Consumer.Manager;
using TGF.Common.Extensions;

namespace TGF.CA.Infrastructure.Comm.Consumer.Host;

/// <summary>
/// Opt-in variant of <see cref="ConsumerHostedService{TMessage}"/> whose consumption is delayed until an application supplied readiness gate allows it.
/// </summary>
/// <remarks>
/// <see cref="StartAsync"/> never waits for the gate. The owned execution task awaits the gate FIRST and only then resolves the underlying
/// <see cref="IMessageConsumer{T}"/> (so its connection, channel, subscription and handler graph are never created while the gate is closed) and runs
/// the same consumption/retry loop as the default service. A gate that returns <c>false</c> or is cancelled means the consumer is never activated.
/// Stopping cancels and awaits the owned task; the underlying consumer is never resolved just to be stopped.
/// </remarks>
public sealed class GatedConsumerHostedService<TMessage> : IHostedService, IDisposable {
    private readonly IConsumerManager<TMessage> _consumerManager;
    private readonly IServiceProvider _serviceProvider;
    private readonly IRetryUtility _retryUtility;
    private readonly Func<IServiceProvider, CancellationToken, Task<bool>> _activationGate;
    private readonly ILogger<GatedConsumerHostedService<TMessage>> _logger;
    private readonly CancellationTokenSource _stoppingCancellationTokenSource = new();
    private Task? _executingTask;

    public GatedConsumerHostedService(
        IConsumerManager<TMessage> consumerManager,
        IServiceProvider serviceProvider,
        IRetryUtility retryUtility,
        Func<IServiceProvider, CancellationToken, Task<bool>> activationGate,
        ILogger<GatedConsumerHostedService<TMessage>> logger) {
        _consumerManager = consumerManager;
        _serviceProvider = serviceProvider;
        _retryUtility = retryUtility;
        _activationGate = activationGate;
        _logger = logger;
    }

    public Task StartAsync(CancellationToken cancellationToken) {
        _executingTask = ExecuteAsync(_stoppingCancellationTokenSource.Token);
        return Task.CompletedTask;
    }

    public async Task StopAsync(CancellationToken cancellationToken) {
        _stoppingCancellationTokenSource.Cancel();
        _consumerManager.StopExecution();
        if (_executingTask is not null) {
            await _executingTask.WaitAsync(cancellationToken);
        }
    }

    public void Dispose() => _stoppingCancellationTokenSource.Dispose();

    // Every outcome is observed here: the task is owned by StopAsync and never faults unobserved.
    private async Task ExecuteAsync(CancellationToken stoppingToken) {
        try {
            var activate = await _activationGate(_serviceProvider, stoppingToken);
            if (!activate || stoppingToken.IsCancellationRequested) {
                return;
            }

            var messageConsumer = _serviceProvider.GetRequiredService<IMessageConsumer<TMessage>>();
            await _retryUtility.ExecuteWithRetryAsync(
                async () => {
                    await ConsumeMessages(messageConsumer, stoppingToken);
                    return true;
                },
                _ => false,
                aMaxRetries: 10,
                aDelayMilliseconds: 2000,
                stoppingToken);
        }
        catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested) {
            // the host is stopping
        }
        catch (Exception) {
            // Fixed text only: provider/handler graph failures can carry connection details.
            _logger.LogError("The gated message consumer stopped after a failure and will not consume messages.");
        }
    }

    private async Task ConsumeMessages(IMessageConsumer messageConsumer, CancellationToken cancellationToken) {
        while (!cancellationToken.IsCancellationRequested) {
            var ct = _consumerManager.GetCancellationToken();
            if (ct.IsCancellationRequested) break;
            try {
                await messageConsumer.StartAsync(cancellationToken);
            }
            catch (OperationCanceledException) {
                // ignore, the operation is getting cancelled
            }
        }
    }
}
