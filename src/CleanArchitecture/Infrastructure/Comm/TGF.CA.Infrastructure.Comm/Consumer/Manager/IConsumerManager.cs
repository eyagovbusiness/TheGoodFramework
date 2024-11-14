namespace TGF.CA.Infrastructure.Comm.Consumer.Manager;

public interface IConsumerManager<TMessage> {
    void RestartExecution();
    void StopExecution();
    CancellationToken GetCancellationToken();
}