using RabbitMQ.Client;
using TGF.CA.Infrastructure.Comm.Consumer;
using TGF.CA.Infrastructure.Comm.Consumer.Handler;
using TGF.CA.Infrastructure.Comm.Messages;
using TGF.CA.Infrastructure.Comm.RabbitMQ;
using TGF.CA.Infrastructure.Comm.RabbitMQ.Settings;
using TGF.Common.Extensions;
using ISerializer = TGF.Common.Serialization.ISerializer;


namespace TGF.CA.Infrastructure.Comm.RabbitMQ.Consumer;
public class RabbitMQMessageConsumer<TMessage> : IMessageConsumer<TMessage> {
    private readonly ISerializer _serializer;
    private readonly IRabbitMQSettingsFactory _rabbitMQSettingsFactory;
    private readonly Lazy<Task<RabbitMQSettings>> _settings;
    private readonly Lazy<Task<ConnectionFactory>> _connectionFactory;
    private readonly IHandleMessage _handleMessage;

    public RabbitMQMessageConsumer(ISerializer aSerializer, IRabbitMQSettingsFactory aRabbitMQSettingsFactory, IHandleMessage aHandleMessage) {
        _serializer = aSerializer;
        _rabbitMQSettingsFactory = aRabbitMQSettingsFactory;
        _settings = new Lazy<Task<RabbitMQSettings>>(_rabbitMQSettingsFactory.GetRabbitMQSettingsAsync);
        _connectionFactory = new Lazy<Task<ConnectionFactory>>(GetConnectionFactory);
        _handleMessage = aHandleMessage;
    }

    public async Task StartAsync(CancellationToken aCancellationToken = default) {
        await Consume(aCancellationToken);
    }

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
        var lReceiver = new RabbitMQMessageReceiver(lChannel, _serializer, _handleMessage);
        string lQueue = await GetCorrectQueue();

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

    private async Task<string> GetCorrectQueue() {
        return (typeof(TMessage) == typeof(IntegrationMessage)
                   ? (await _settings.Value).Consumer?.IntegrationQueue
                   : (await _settings.Value).Consumer?.DomainQueue)
               ?? throw new ArgumentException("Please configure the queues in the app settings.");
    }

    private async Task<ConnectionFactory> GetConnectionFactory() {
        var lSettings = await _settings.Value;
        return new() {
            HostName = lSettings.Hostname,
            Password = lSettings.Credentials!.Password,
            UserName = lSettings.Credentials!.Username
        };
    }
}


