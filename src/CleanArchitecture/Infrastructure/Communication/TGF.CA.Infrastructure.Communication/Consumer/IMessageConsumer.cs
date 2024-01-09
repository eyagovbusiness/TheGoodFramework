namespace TGF.CA.Infrastructure.Communication.Consumer;

public interface IMessageConsumer
{
    Task StartAsync(CancellationToken cancelToken = default);
}

public interface IMessageConsumer<T> : IMessageConsumer
{
}