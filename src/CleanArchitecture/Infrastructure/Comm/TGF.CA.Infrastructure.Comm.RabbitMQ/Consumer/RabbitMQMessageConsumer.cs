using Microsoft.Extensions.Configuration;
using RabbitMQ.Client;
using TGF.CA.Infrastructure.Comm.Consumer;
using TGF.CA.Infrastructure.Comm.Consumer.Handler;
using TGF.CA.Infrastructure.Comm.Messages;
using TGF.CA.Infrastructure.Comm.RabbitMQ.Settings;
using TGF.Common.Extensions;
using ISerializer = TGF.Common.Serialization.ISerializer;

namespace TGF.CA.Infrastructure.Comm.RabbitMQ.Consumer;

public class RabbitMQMessageConsumer<TMessage>(ISerializer serializer, IRabbitMQSettingsFactory rabbitMQSettingsFactory, IHandleMessage handleMessage, IConfiguration configuration)
: IMessageConsumer<TMessage> {
    private readonly Lazy<Task<RabbitMQSettings>> _settings = new(rabbitMQSettingsFactory.GetRabbitMQSettingsAsync);
    private readonly Lazy<Task<ConnectionFactory>> _connectionFactory = new(() => GetConnectionFactory(rabbitMQSettingsFactory, configuration));

    public async Task StartAsync(CancellationToken aCancellationToken = default)
    => await Consume(aCancellationToken);

    private async Task Consume(CancellationToken aCancellationToken) {
        var lConnectionFactory = await _connectionFactory.Value;

        using var lConnection = await RetryUtility.ExecuteWithRetryAsync(
            async () => {
                // Wrapping the connection creation in retry logic.
                return await Task.Run(() => lConnectionFactory.CreateConnection(), aCancellationToken);
            },
            _ => false, // Retry only on exceptions.
            aMaxRetries: 10, // Customize max retries as needed.
            aDelayMilliseconds: 2000, // Customize delay between retries.
            aCancellationToken // Pass the provided CancellationToken.
        );

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

    private static async Task<ConnectionFactory> GetConnectionFactory(IRabbitMQSettingsFactory settingsFactory, IConfiguration configuration) {
        var settings = await settingsFactory.GetRabbitMQSettingsAsync();
        return new() {
            Uri = new Uri(settings.GetConnectionString(configuration))
        };
    }

}


