
namespace TGF.CA.Application.Contracts.Communication
{
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
}
