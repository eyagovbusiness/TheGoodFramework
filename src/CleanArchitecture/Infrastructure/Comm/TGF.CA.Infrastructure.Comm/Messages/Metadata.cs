namespace TGF.CA.Infrastructure.Communication.Messages;

public record Metadata
{
    public string CorrelationId { get; }
    public DateTimeOffset CreatedAt { get; }

    public Metadata(string aCorrelationId, DateTimeOffset aCreatedAt)
    {
        CorrelationId = aCorrelationId;
        CreatedAt = aCreatedAt;
    }
}