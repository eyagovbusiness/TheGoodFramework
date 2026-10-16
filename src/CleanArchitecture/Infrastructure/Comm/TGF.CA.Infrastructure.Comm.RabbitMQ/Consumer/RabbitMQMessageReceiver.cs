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
internal class RabbitMQMessageReceiver(IModel channel, ISerializer serializer, IHandleMessage handleMessage, ILogger logger, int maxHandlerRetries) : DefaultBasicConsumer(channel) {
    public override void HandleBasicDeliver(string consumerTag, ulong deliveryTag, bool redelivered, string exchange, string routingKey, IBasicProperties properties, ReadOnlyMemory<byte> body) {
        var messageBody = body.ToArray();
        var messageType = Type.GetType(properties.Type);

        if (messageType == null) {
            logger.LogWarning("Cannot resolve message type: {Type}", properties.Type);
            channel.BasicNack(deliveryTag, false, false);
            return;
        }

        logger.LogInformation("Received message. ConsumerTag: {ConsumerTag}, DeliveryTag: {DeliveryTag}, Type: {Type}", consumerTag, deliveryTag, messageType.FullName);

        _ = Task.Run(async () => {
            try {
                var message = serializer.DeserializeObject(messageBody, messageType) as IMessage;
                if (message == null) {
                    logger.LogWarning("Message deserialized as null. DeliveryTag: {DeliveryTag}", deliveryTag);
                    channel.BasicNack(deliveryTag, false, false);
                    return;
                }

                logger.LogInformation("Handling message: {MessageIdentifier}", message.MessageIdentifier);

                var attempts = Math.Max(1, maxHandlerRetries);
                for (int attempt = 1; attempt <= attempts; attempt++) {
                    try {
                        await handleMessage.Handle(message, CancellationToken.None);
                        channel.BasicAck(deliveryTag, false);
                        return;
                    }
                    catch (Exception ex) when (attempt < attempts) {
                        // Handler exceptions (repository/driver/broker) can carry connection details and values: only the type is logged.
                        logger.LogWarning(
                            "Error processing message {MessageIdentifier}. Attempt {Attempt}/{MaxAttempts}. Retrying. Exception type: {ExceptionType}",
                            message.MessageIdentifier,
                            attempt,
                            attempts,
                            ex.GetType().Name);
                    }
                }

                throw new InvalidOperationException($"Message {message.MessageIdentifier} failed after {attempts} attempts.");
            }
            catch (Exception ex) {
                logger.LogError("Error processing message. DeliveryTag: {DeliveryTag}. Dead-lettering after {MaxRetries} failed attempts. Exception type: {ExceptionType}", deliveryTag, Math.Max(1, maxHandlerRetries), ex.GetType().Name);
                channel.BasicNack(deliveryTag, false, false);
            }
        });
    }
}
