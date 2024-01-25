using RabbitMQ.Client;
using TGF.CA.Infrastructure.Communication.Consumer.Handler;
using TGF.CA.Infrastructure.Communication.Messages;
using TGF.Common.Serialization;

namespace TGF.CA.Infrastructure.Communication.RabbitMQ.Consumer;

public class RabbitMQMessageReceiver : DefaultBasicConsumer
{
    private readonly IModel _channel;
    private readonly ISerializer _serializer;
    private readonly IHandleMessage _handleMessage;
    private byte[]? MessageBody { get; set; }
    private Type? MessageType { get; set; }
    private ulong DeliveryTag { get; set; }

    public RabbitMQMessageReceiver(IModel channel, ISerializer serializer, IHandleMessage handleMessage)
    {
        _channel = channel;
        _serializer = serializer;
        _handleMessage = handleMessage;
    }

    public override async void HandleBasicDeliver(string consumerTag, ulong deliveryTag, bool redelivered, string exchange,
        string routingKey, IBasicProperties properties, ReadOnlyMemory<byte> body)
    {
        await Task.Yield(); // Allows the handler to resume on a different thread if it's able to, potentially freeing up the current thread

        MessageType = Type.GetType(properties.Type)!;
        MessageBody = body.ToArray();
        DeliveryTag = deliveryTag;

        try
        {
            await HandleMessage();
            _channel.BasicAck(DeliveryTag, false);  // Acknowledge the message reception to remove it from the queue
        }
        catch (Exception ex)
        {
            // Here, you should decide how to handle the exception. You might want to log it,
            // and you might decide to either acknowledge or not acknowledge the message depending on the nature of the error.
            Console.WriteLine($"An error occurred while processing the message: {ex.Message}");
            _channel.BasicNack(DeliveryTag, false, true);  // This is just an example to not acknowledge the message and let it be requeued
        }
    }

    private async Task HandleMessage()
    {
        if (MessageBody == null || MessageType == null)
        {
            throw new ArgumentException("Neither the body nor the messageType have been populated");
        }

        var message = (_serializer.DeserializeObject(MessageBody, MessageType) as IMessage)
                      ?? throw new ArgumentException("The message didn't deserialize properly");

        await _handleMessage.Handle(message, CancellationToken.None);
    }
}

