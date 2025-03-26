using Microsoft.Extensions.Logging;
using RabbitMQ.Client;
using TGF.CA.Infrastructure.Comm.Consumer.Handler;
using TGF.CA.Infrastructure.Comm.Messages;
using TGF.Common.Serialization;

namespace TGF.CA.Infrastructure.Comm.RabbitMQ.Consumer;

/// <summary>
/// Class to receive messages from RabbitMQ. It is a consumer that listens to a queue and processes the messages with a custom handler for handlign the basic deliver.
/// </summary>
/// <param name="channel"></param>
/// <param name="serializer"></param>
/// <param name="handleMessage"></param>
internal class RabbitMQMessageReceiver(IModel channel, ISerializer serializer, IHandleMessage handleMessage, ILogger logger)
: DefaultBasicConsumer {
    private byte[]? MessageBody { get; set; }
    private Type? MessageType { get; set; }
    private ulong DeliveryTag { get; set; }

    public override async void HandleBasicDeliver(string consumerTag, ulong deliveryTag, bool redelivered, string exchange,
        string routingKey, IBasicProperties properties, ReadOnlyMemory<byte> body) {
        await Task.Yield(); // Allows the handler to resume on a different thread if it's able to, potentially freeing up the current thread

        MessageType = Type.GetType(properties.Type)!;
        MessageBody = body.ToArray();
        DeliveryTag = deliveryTag;

        try {
            await HandleMessage();
            channel.BasicAck(DeliveryTag, false);  // Acknowledge the message reception to remove it from the queue
        }
        catch (Exception ex) {
            // Here, you should decide how to handle the exception. You might want to log it,
            // and you might decide to either acknowledge or not acknowledge the message depending on the nature of the error.
            logger.LogError(ex, "An error occurred while processing the message");
            channel.BasicNack(DeliveryTag, false, true);  // This is just an example to not acknowledge the message and let it be requeued
        }
    }

    private async Task HandleMessage() {
        if (MessageBody == null || MessageType == null) {
            throw new ArgumentException("Neither the body nor the messageType have been populated");
        }

        var message = serializer.DeserializeObject(MessageBody, MessageType) as IMessage
                      ?? throw new ArgumentException("The message didn't deserialize properly");

        await handleMessage.Handle(message, CancellationToken.None);
    }
}

