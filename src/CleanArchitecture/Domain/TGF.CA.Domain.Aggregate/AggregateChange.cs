
namespace TGF.CA.Domain.Aggregate
{
    public record AggregateChange<TKey>(
        object Content,
        TKey Id, 
        Type Type, 
        string TransactionId, 
        int Version, 
        bool IsNew
    ) where TKey : struct, IEquatable<TKey>;

    //Stored in DB
    public class AggregateChangeEntity<TKey>(
        TKey aAggregateId,
        object aContent,
        string aAggregateType,
        string aTransactionId,
        int aAggregateVersion) 
        where TKey : struct, IEquatable<TKey>
    {
        public TKey AggregateId { get; init; } = aAggregateId;
        public object Content { get; init; } = aContent;

        public string AggregateType { get; init; } = aAggregateType;
        public string TransactionId { get; init; } = aTransactionId;
        public int AggregateVersion { get; init; } = aAggregateVersion;
    }
}
