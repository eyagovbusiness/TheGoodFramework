using RabbitMQ.Client;
using TGF.CA.Infrastructure.Comm.Consumer;
using TGF.CA.Infrastructure.Comm.Consumer.Handler;
using TGF.CA.Infrastructure.Comm.Messages;
using TGF.CA.Infrastructure.Comm.RabbitMQ.Connection;
using TGF.CA.Infrastructure.Comm.RabbitMQ.Settings;
using ISerializer = TGF.Common.Serialization.ISerializer;

namespace TGF.CA.Infrastructure.Comm.RabbitMQ.Consumer;

internal class RabbitMQMessageConsumer<TMessage>(RabbitMQSettings rabbitMQSettings, IRabbitMQConnectionFactory rabbitMQConnectionFactory, IHandleMessage handleMessage, ISerializer serializer)
: IMessageConsumer<TMessage> {

    public async Task StartAsync(CancellationToken aCancellationToken = default)
    => await Consume(aCancellationToken);

    private async Task Consume(CancellationToken aCancellationToken) {
        using var lConnection = await rabbitMQConnectionFactory.GetConnectionAsync();

        using var lChannel = lConnection.CreateModel();
        lChannel.BasicQos(0, 1, false); // Each consumer will take only 1 message at a time and the next after ACK.
        var lReceiver = new RabbitMQMessageReceiver(lChannel, serializer, handleMessage);
        var lQueue = GetCorrectQueue();

        lChannel.BasicConsume(lQueue, false, lReceiver);

        await WaitUntilCancelled(aCancellationToken);
    }

    private static async Task WaitUntilCancelled(CancellationToken aCancellationToken) {
        var lTaskCompletionSource = new TaskCompletionSource<bool>();
        using (aCancellationToken.Register(x => {
            if (x is TaskCompletionSource<bool> lTaskCompletionSource)
                lTaskCompletionSource.SetResult(true);
        }, lTaskCompletionSource)) {
            await lTaskCompletionSource.Task;
        }
    }

    private string GetCorrectQueue()
    => (typeof(TMessage) == typeof(IntegrationMessage)
        ? rabbitMQSettings.Consumer?.IntegrationQueue
        : rabbitMQSettings.Consumer?.DomainQueue)
    ?? throw new ArgumentException("Please configure the queues in the app settings.");

}


