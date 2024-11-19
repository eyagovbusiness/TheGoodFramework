using Microsoft.Extensions.Hosting;
using TGF.CA.Infrastructure.Comm.Consumer;
using TGF.CA.Infrastructure.Comm.Consumer.Manager;
using TGF.Common.Extensions;

namespace TGF.CA.Infrastructure.Comm.Consumer.Host;

public class ConsumerHostedService<TMessage> : IHostedService {
    private readonly IConsumerManager<TMessage> _consumerManager;
    private readonly IMessageConsumer<TMessage> _messageConsumer;
    private readonly CancellationTokenSource _stoppingCancellationTokenSource = new();
    private Task? _executingTask;

    public ConsumerHostedService(IConsumerManager<TMessage> consumerManager, IMessageConsumer<TMessage> messageConsumer) {
        _consumerManager = consumerManager;
        _messageConsumer = messageConsumer;
    }

    public Task StartAsync(CancellationToken cancellationToken) {
        _executingTask = RetryUtility.ExecuteWithRetryAsync(
            async () => {
                await ConsumeMessages(_stoppingCancellationTokenSource.Token);
                return true; // Return a value to satisfy the generic signature.
            },
            _ => false, // Retry only on exceptions.
            aMaxRetries: 10, // Customize max retries as needed.
            aDelayMilliseconds: 2000, // Customize delay between retries.
            cancellationToken // Pass the provided CancellationToken for proper handling.
        );

        return _executingTask.IsCompleted ? _executingTask : Task.CompletedTask;
    }


    public Task StopAsync(CancellationToken cancellationToken) {
        _stoppingCancellationTokenSource.Cancel();
        _consumerManager.StopExecution();
        return Task.CompletedTask;
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
            //#3 investigate if an exception on the process breaks the consumer.
        }
    }
}

