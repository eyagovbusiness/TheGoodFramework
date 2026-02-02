
namespace TGF.CA.Application.Contracts.Communication {
    public record Metadata {
        public string CorrelationId { get; }
        public DateTimeOffset CreatedAt { get; }
        public byte? Priority { get; }

        public Metadata(string aCorrelationId, DateTimeOffset aCreatedAt, byte? priority = null) {
            CorrelationId = aCorrelationId;
            CreatedAt = aCreatedAt;
            Priority = priority;
        }
    }
}
