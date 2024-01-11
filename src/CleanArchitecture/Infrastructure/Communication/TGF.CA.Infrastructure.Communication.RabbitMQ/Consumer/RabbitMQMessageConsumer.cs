using RabbitMQ.Client;
using TGF.CA.Infrastructure.Communication.Consumer;
using TGF.CA.Infrastructure.Communication.Consumer.Handler;
using TGF.CA.Infrastructure.Communication.Messages;
using TGF.CA.Infrastructure.Communication.RabbitMQ.Settings;
using ISerializer = TGF.Common.Serialization.ISerializer;


namespace TGF.CA.Infrastructure.Communication.RabbitMQ.Consumer;
public class RabbitMQMessageConsumer<TMessage> : IMessageConsumer<TMessage>
{
    private readonly ISerializer _serializer;
    private readonly IRabbitMQSettingsFactory _rabbitMQSettingsFactory;
    private readonly Lazy<Task<RabbitMQSettings>> _settings;
    private readonly Lazy<Task<ConnectionFactory>> _connectionFactory;
    private readonly IHandleMessage _handleMessage;

    public RabbitMQMessageConsumer(ISerializer aSerializer, IRabbitMQSettingsFactory aRabbitMQSettingsFactory, IHandleMessage aHandleMessage)
    {
        _serializer = aSerializer;
        _rabbitMQSettingsFactory = aRabbitMQSettingsFactory;
        _settings = new Lazy<Task<RabbitMQSettings>>(_rabbitMQSettingsFactory.GetRabbitMQSettingsAsync);
        _connectionFactory = new Lazy<Task<ConnectionFactory>>(GetConnectionFactory);
        _handleMessage = aHandleMessage;
    }

    public async Task StartAsync(CancellationToken aCancellationToken = default)
    {
        await Consume(aCancellationToken);
    }

    private async Task Consume(CancellationToken aCancellationToken)
    {
        var lConnectionFactory = await _connectionFactory.Value;
        using var lConnection = lConnectionFactory.CreateConnection();
        using var lChannel = lConnection.CreateModel();
        lChannel.BasicQos(0, 1, false);//Each consumer will take only 1 message at time and take the next one after ACK the current processing one.
        var lReceiver = new RabbitMQMessageReceiver(lChannel, _serializer, _handleMessage);
        string lQueue = await GetCorrectQueue();

        lChannel.BasicConsume(lQueue, false, lReceiver);

        await WaitUntilCancelled(aCancellationToken);
    }

    private static async Task WaitUntilCancelled(CancellationToken aCancellationToken)
    {
        var lTaskCompletionSource = new TaskCompletionSource<bool>();
        using (aCancellationToken.Register(x =>
        {
            if (x is TaskCompletionSource<bool> lTaskCompletionSource)
                lTaskCompletionSource.SetResult(true);
        }, lTaskCompletionSource))
        {
            await lTaskCompletionSource.Task;
        }
    }

    private async Task<string> GetCorrectQueue()
    {
        return (typeof(TMessage) == typeof(IntegrationMessage)
                   ? (await _settings.Value).Consumer?.IntegrationQueue
                   : (await _settings.Value).Consumer?.DomainQueue)
               ?? throw new ArgumentException("Please configure the queues in the app settings.");
    }

    private async Task<ConnectionFactory> GetConnectionFactory()
    {
        var lSettings = await _settings.Value;
        return new()
        {
            HostName = lSettings.Hostname,
            Password = lSettings.Credentials!.Password,
            UserName = lSettings.Credentials!.Username
        };
    }
}


