namespace TGF.CA.Infrastructure.Communication.Consumer;
//CODE FROM https://github.com/ElectNewt/Distribt
public interface IMessageConsumer
{
    Task StartAsync(CancellationToken cancelToken = default);
}

public interface IMessageConsumer<T> : IMessageConsumer
{
}