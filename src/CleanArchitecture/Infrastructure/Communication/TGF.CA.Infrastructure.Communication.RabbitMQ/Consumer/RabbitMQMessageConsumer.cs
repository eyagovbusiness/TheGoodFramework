using Microsoft.Extensions.Options;
using RabbitMQ.Client;
using TGF.CA.Infrastructure.Communication.Consumer;
using TGF.CA.Infrastructure.Communication.Consumer.Handler;
using TGF.CA.Infrastructure.Communication.Messages;
using ISerializer = TGF.Common.Serialization.ISerializer;

namespace TGF.CA.Infrastructure.Communication.RabbitMQ.Consumer;
public class RabbitMQMessageConsumer<TMessage> : IMessageConsumer<TMessage>
{
    private readonly ISerializer _serializer;
    private readonly RabbitMQSettings _settings;
    private readonly ConnectionFactory _connectionFactory;
    private readonly IHandleMessage _handleMessage;

    public RabbitMQMessageConsumer(ISerializer aSerializer, IOptions<RabbitMQSettings> aSettings, IHandleMessage aHandleMessage)
    {
        _settings = aSettings.Value;
        _serializer = aSerializer;
        _handleMessage = aHandleMessage;
        _connectionFactory = new ConnectionFactory
        {
            HostName = _settings.Hostname,
            Password = _settings.Credentials?.Password,
            UserName = _settings.Credentials?.Username
        };
    }

    public async Task StartAsync(CancellationToken aCancellationToken = default)
    {
        await Consume(aCancellationToken);
    }

    private async Task Consume(CancellationToken aCancellationToken)
    {
        using var lConnection = _connectionFactory.CreateConnection();
        using var lChannel = lConnection.CreateModel();
        var lReceiver = new RabbitMQMessageReceiver(lChannel, _serializer, _handleMessage);
        string lQueue = GetCorrectQueue();

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

    private string GetCorrectQueue()
    {
        return (typeof(TMessage) == typeof(IntegrationMessage)
                   ? _settings.Consumer?.IntegrationQueue
                   : _settings.Consumer?.DomainQueue)
               ?? throw new ArgumentException("Please configure the queues in the app settings.");
    }
}


