namespace TGF.CA.Infrastructure.Comm.Consumer;

public interface IMessageConsumer {
    Task StartAsync(CancellationToken cancelToken = default);
}

public interface IMessageConsumer<T> : IMessageConsumer {
}