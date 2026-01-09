using Microsoft.Extensions.DependencyInjection;
using TGF.CA.Infrastructure.Comm.Messages;

namespace TGF.CA.Infrastructure.Comm.Consumer.Handler;

/// <summary>
/// Base class for message handlers that need to work with scoped services.
/// Automatically creates a new service scope for each message to ensure fresh DbContext instances.
/// </summary>
/// <typeparam name="TMessage">The type of message this handler processes</typeparam>
/// <remarks>
/// MESSAGE HANDLER SCOPING PATTERN:
/// 
/// Message handlers are registered as Transient but may be reused by the consumer infrastructure.
/// To prevent "captive dependency" issues with scoped services (like DbContext/Repositories):
/// 
/// 1. DO NOT inject scoped services directly in the constructor
/// 2. DO inject IServiceProvider
/// 3. DO create a new scope in Handle() method
/// 4. DO resolve scoped services from the new scope
/// 
/// This ensures each message gets a fresh DbContext with no cached/stale entities.
/// 
/// Example Usage:
/// <code>
/// public class MyMessageHandler : ScopedMessageHandlerBase&lt;MyMessage&gt;
/// {
///     public MyMessageHandler(IServiceProvider serviceProvider, ILogger&lt;MyMessageHandler&gt; logger) 
///         : base(serviceProvider, logger) { }
/// 
///     protected override async Task HandleScoped(MyMessage message, IServiceProvider scopedServices, CancellationToken cancellationToken)
///     {
///         var repository = scopedServices.GetRequiredService&lt;IMyRepository&gt;();
///         // ... handle message with fresh repository
///     }
/// }
/// </code>
/// </remarks>
public abstract class ScopedMessageHandlerBase<TMessage> : IMessageHandler<TMessage> 
    where TMessage : IMessage
{
    private readonly IServiceProvider _serviceProvider;
    protected readonly Microsoft.Extensions.Logging.ILogger Logger;

    protected ScopedMessageHandlerBase(
        IServiceProvider serviceProvider, 
        Microsoft.Extensions.Logging.ILogger logger)
    {
        _serviceProvider = serviceProvider;
        Logger = logger;
    }

    /// <summary>
    /// Implements the IMessageHandler interface by creating a new scope and delegating to HandleScoped.
    /// DO NOT OVERRIDE THIS METHOD - override HandleScoped instead.
    /// </summary>
    public async Task Handle(TMessage message, CancellationToken cancellationToken = default)
    {
        using var scope = _serviceProvider.CreateScope();
        await HandleScoped(message, scope.ServiceProvider, cancellationToken);
    }

    /// <summary>
    /// Override this method to implement the message handling logic.
    /// All scoped services should be resolved from the scopedServices parameter.
    /// </summary>
    /// <param name="message">The message to handle</param>
    /// <param name="scopedServices">Service provider for the current scope - use this to resolve repositories and other scoped services</param>
    /// <param name="cancellationToken">Cancellation token</param>
    protected abstract Task HandleScoped(TMessage message, IServiceProvider scopedServices, CancellationToken cancellationToken);
}
