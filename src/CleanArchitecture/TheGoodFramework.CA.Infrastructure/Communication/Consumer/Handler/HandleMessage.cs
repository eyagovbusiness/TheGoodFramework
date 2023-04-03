using System.Reflection;
using TGF.CA.Infrastructure.Communication.Messages;
//CODE FROM https://github.com/ElectNewt/Distribt
namespace TGF.CA.Infrastructure.Communication.Consumer.Handler;

public interface IHandleMessage
{ 
    Task Handle(IMessage message, CancellationToken cancellationToken = default);
}

public class HandleMessage  : IHandleMessage
{
    private readonly IMessageHandlerRegistry _messageHandlerRegistry;

    public HandleMessage(IMessageHandlerRegistry messageHandlerRegistry)
    {
        _messageHandlerRegistry = messageHandlerRegistry;
    }

    public Task Handle(IMessage message, CancellationToken cancellationToken = default)
    {
        if (message == null) throw new ArgumentNullException(nameof(message));

        Type messageType = message.GetType();
        var handlerType = typeof(IMessageHandler<>).MakeGenericType(messageType);
        List<IMessageHandler> handlers = _messageHandlerRegistry.GetMessageHandlerForType(handlerType, messageType).ToList();

        foreach (IMessageHandler handler in handlers)
        {
            Type messageHandlerType = handler.GetType();
            
            MethodInfo? handle = messageHandlerType.GetMethods()
                .Where(methodInfo => methodInfo.Name == nameof(IMessageHandler<object>.Handle))
                .FirstOrDefault(info => info.GetParameters()
                    .Select(parameter => parameter.ParameterType)
                    .Contains(message.GetType()));
            
            if (handle != null) 
                return  (Task) handle.Invoke(handler, new object[] {message, cancellationToken})!;
        }
        return  Task.CompletedTask;
    }
}