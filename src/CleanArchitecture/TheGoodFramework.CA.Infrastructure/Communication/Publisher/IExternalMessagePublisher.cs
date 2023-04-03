using TGF.CA.Infrastructure.Communication.Messages;

namespace  TGF.CA.Infrastructure.Communication.Publisher;
//CODE FROM https://github.com/ElectNewt/Distribt
public interface IExternalMessagePublisher<in TMessage>
    where TMessage : IMessage
{
    Task Publish(TMessage message, string? routingKey = null, CancellationToken cancellationToken = default);
    Task PublishMany(IEnumerable<TMessage> messages, string? routingKey = null, CancellationToken cancellationToken = default);
}