namespace TGF.CA.Infrastructure.Comm.Messages;

public interface IMessage {
    /// <summary>
    /// Must be unique;
    /// </summary>
    public string MessageIdentifier { get; }
    /// <summary>
    /// Name for the message, useful in logs/databases, etc
    /// </summary>
    public string Name { get; }
}