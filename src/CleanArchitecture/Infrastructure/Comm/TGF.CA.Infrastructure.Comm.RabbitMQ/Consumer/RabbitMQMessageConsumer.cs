using Microsoft.Extensions.Logging;
using RabbitMQ.Client;
using TGF.CA.Infrastructure.Comm.Consumer;
using TGF.CA.Infrastructure.Comm.Consumer.Handler;
using TGF.CA.Infrastructure.Comm.Messages;
using TGF.CA.Infrastructure.Comm.RabbitMQ.Connection;
using TGF.CA.Infrastructure.Comm.RabbitMQ.Settings;
using ISerializer = TGF.Common.Serialization.ISerializer;

namespace TGF.CA.Infrastructure.Comm.RabbitMQ.Consumer;

/// <summary>
/// RabbitMQ message consumer.
/// </summary>
/// <typeparam name="TMessage">The Type of the message this consumer will be in charge of consuming.</typeparam>
internal class RabbitMQMessageConsumer<TMessage>(RabbitMQSettings rabbitMQSettings, IRabbitMQConnectionFactory rabbitMQConnectionFactory, IHandleMessage handleMessage, ISerializer serializer, ILogger<RabbitMQMessageConsumer<TMessage>> logger)
: IMessageConsumer<TMessage> {

    /// <summary>
    /// Start consuming messages.
    /// </summary>
    public async Task StartAsync(CancellationToken aCancellationToken = default)
    => await Consume(aCancellationToken);

    #region private
    private async Task Consume(CancellationToken aCancellationToken) {
        // NOTE: the connection is owned by the factory and shared with the publishers and the health checks.
        // It must NOT be disposed here. Disposing it used to kill the shared connection for the whole process:
        // every later caller then got an ObjectDisposedException, retries could never recover, and the service
        // stayed unready until it was restarted. Only the channel below is ours to dispose.
        var lConnection = await rabbitMQConnectionFactory.GetConnectionAsync();

        using var lChannel = lConnection.CreateModel();
        var lCompletion = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
        // A replacement connection cannot recover this channel. Unwind so the host subscribes on the current connection.
        void OnChannelShutdown(object? sender, ShutdownEventArgs args)
            => lCompletion.TrySetException(new InvalidOperationException("The RabbitMQ consumer channel closed."));

        lChannel.ModelShutdown += OnChannelShutdown;
        try {
            lChannel.BasicQos(0, 1, false); // Each consumer will take only 1 message at a time and the next after ACK.
            var lReceiver = new RabbitMQMessageReceiver(lChannel, serializer, handleMessage, logger, rabbitMQSettings.Consumer?.MaxHandlerRetries ?? 3);
            var lQueue = GetCorrectQueue();

            lChannel.BasicConsume(lQueue, false, lReceiver);

            using var lCancellationRegistration = aCancellationToken.Register(() => lCompletion.TrySetResult(true));
            await lCompletion.Task;
        }
        finally {
            lChannel.ModelShutdown -= OnChannelShutdown;
        }
    }

    private string GetCorrectQueue()
    => (typeof(TMessage) == typeof(IntegrationMessage)
        ? rabbitMQSettings.Consumer?.IntegrationQueue
        : rabbitMQSettings.Consumer?.DomainQueue)
    ?? throw new ArgumentException("Please configure the queues in the app settings.");
    #endregion

}

