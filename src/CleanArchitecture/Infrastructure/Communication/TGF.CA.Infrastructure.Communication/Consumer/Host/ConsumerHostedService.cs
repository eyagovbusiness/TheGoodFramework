using Microsoft.Extensions.Hosting;
using TGF.CA.Infrastructure.Communication.Consumer.Manager;

namespace TGF.CA.Infrastructure.Communication.Consumer.Host;
//CODE FROM https://github.com/ElectNewt/Distribt
public class ConsumerHostedService<TMessage> : IHostedService
{
    private readonly IConsumerManager<TMessage> _consumerManager;
    private readonly IMessageConsumer<TMessage> _messageConsumer;
    private readonly CancellationTokenSource _stoppingCancellationTokenSource = new();
    private Task? _executingTask;

    public ConsumerHostedService(IConsumerManager<TMessage> consumerManager, IMessageConsumer<TMessage> messageConsumer)
    {
        _consumerManager = consumerManager;
        _messageConsumer = messageConsumer;
    }

    public Task StartAsync(CancellationToken cancellationToken)
    {
        _executingTask = ConsumeMessages(_stoppingCancellationTokenSource.Token);

        return _executingTask.IsCompleted ? _executingTask : Task.CompletedTask;
    }

    public Task StopAsync(CancellationToken cancellationToken)
    {
        _stoppingCancellationTokenSource.Cancel();
        _consumerManager.StopExecution();
        return Task.CompletedTask;
    }

    private async Task ConsumeMessages(CancellationToken cancellationToken)
    {
        while (!cancellationToken.IsCancellationRequested)
        {
            var ct = _consumerManager.GetCancellationToken();
            if (ct.IsCancellationRequested) break;
            try
            {
                await _messageConsumer.StartAsync(cancellationToken);
            }
            catch (OperationCanceledException)
            {
                // ignore, the operation is getting cancelled
            }
            //#3 investigate if an exception on the process breaks the consumer.
        }
    }
}

