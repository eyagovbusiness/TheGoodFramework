
namespace TGF.CA.Domain.Aggregate
{
    public record AggregateChange(
        object Content, 
        Guid Id, 
        Type Type, 
        string TransactionId, 
        int Version, 
        bool IsNew
    );

    //Stored in DB
    public class AggregateChangeEntity(
        Guid aAggregateId,
        object aContent,
        string aAggregateType,
        string aTransactionId,
        int aAggregateVersion)
    {
        public Guid AggregateId { get; init; } = aAggregateId;
        public object Content { get; init; } = aContent;

        public string AggregateType { get; init; } = aAggregateType;
        public string TransactionId { get; init; } = aTransactionId;
        public int AggregateVersion { get; init; } = aAggregateVersion;
    }
}
