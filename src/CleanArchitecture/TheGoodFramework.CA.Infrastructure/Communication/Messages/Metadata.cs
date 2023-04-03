namespace TGF.CA.Infrastructure.Communication.Messages;
//CODE FROM https://github.com/ElectNewt/Distribt
public record Metadata
{
    public string CorrelationId { get; }
    public DateTime CreatedUtc { get; }

    public Metadata(string correlationId, DateTime createdUtc)
    {
        CorrelationId = correlationId;
        CreatedUtc = createdUtc;
    }
}