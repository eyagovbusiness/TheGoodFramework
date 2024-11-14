using TGF.CA.Application.Contracts.Communication;

namespace TGF.CA.Infrastructure.Comm.Messages;

public record DomainMessage : IMessage {
    public string MessageIdentifier { get; }
    public string Name { get; }

    public DomainMessage(string messageIdentifier, string name) {
        MessageIdentifier = messageIdentifier;
        Name = name;
    }
}

public record DomainMessage<T> : DomainMessage {
    public T Content { get; }
    public Metadata Metadata { get; }

    public DomainMessage(string messageIdentifier, string name, T content, Metadata metadata)
        : base(messageIdentifier, name) {
        Content = content;
        Metadata = metadata;
    }
}