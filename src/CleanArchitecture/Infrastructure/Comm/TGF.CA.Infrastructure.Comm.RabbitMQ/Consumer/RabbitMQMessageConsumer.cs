using RabbitMQ.Client;
using TGF.CA.Infrastructure.Comm.Consumer;
using TGF.CA.Infrastructure.Comm.Consumer.Handler;
using TGF.CA.Infrastructure.Comm.Messages;
using TGF.CA.Infrastructure.Comm.RabbitMQ.Connection;
using TGF.CA.Infrastructure.Comm.RabbitMQ.Settings;
using ISerializer = TGF.Common.Serialization.ISerializer;

namespace TGF.CA.Infrastructure.Comm.RabbitMQ.Consumer;

internal class RabbitMQMessageConsumer<TMessage>(IRabbitMQSettingsFactory rabbitMQSettingsFactory, IRabbitMQConnectionFactory rabbitMQConnectionFactory, IHandleMessage handleMessage, ISerializer serializer)
: IMessageConsumer<TMessage> {
    private readonly Lazy<Task<RabbitMQSettings>> _settings = new(rabbitMQSettingsFactory.GetRabbitMQSettingsAsync);

    public async Task StartAsync(CancellationToken aCancellationToken = default)
    => await Consume(aCancellationToken);

    private async Task Consume(CancellationToken aCancellationToken) {
        using var lConnection = await rabbitMQConnectionFactory.GetConnectionAsync();

        using var lChannel = lConnection.CreateModel();
        lChannel.BasicQos(0, 1, false); // Each consumer will take only 1 message at a time and the next after ACK.
        var lReceiver = new RabbitMQMessageReceiver(lChannel, serializer, handleMessage);
        var lQueue = await GetCorrectQueue();

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

    private async Task<string> GetCorrectQueue()
    => (typeof(TMessage) == typeof(IntegrationMessage)
        ? (await _settings.Value).Consumer?.IntegrationQueue
        : (await _settings.Value).Consumer?.DomainQueue)
    ?? throw new ArgumentException("Please configure the queues in the app settings.");

}


