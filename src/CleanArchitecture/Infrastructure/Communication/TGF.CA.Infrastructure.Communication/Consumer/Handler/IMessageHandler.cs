using TGF.CA.Domain.Messages;
using TGF.CA.Infrastructure.Communication.Messages;

namespace TGF.CA.Infrastructure.Communication.Consumer.Handler;

public interface IMessageHandler
{
}

public interface IMessageHandler<in TMessage> : IMessageHandler
{
    Task Handle(TMessage aMessage, CancellationToken aCancelToken = default);
}

public interface IIntegrationMessageHandler : IMessageHandler
{
}

public interface IIntegrationMessageHandler<TMessageContent>
    : IMessageHandler<IntegrationMessage<TMessageContent>>, IIntegrationMessageHandler
{
}

public interface IDomainMessageHandler : IMessageHandler
{
}

public interface IDomainMessageHandler<TMessageContent>
    : IMessageHandler<DomainMessage<TMessageContent>>, IDomainMessageHandler
{
}