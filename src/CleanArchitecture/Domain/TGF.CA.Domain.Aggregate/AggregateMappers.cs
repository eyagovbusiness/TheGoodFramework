
namespace TGF.CA.Domain.Aggregate
{
    public static class AggregateMappers
    {
        public static AggregateChange<TKey> ToAggregateChange<TKey>(AggregateChangeEntity<TKey> aChange)
            where TKey : struct, IEquatable<TKey>
        => new (
            aChange.Content,
            aChange.AggregateId,
            aChange.GetType(),
            aChange.TransactionId,
            aChange.AggregateVersion,
            false
        );

        public static AggregateChangeEntity<TKey> ToTypedAggregateChangeDto<TKey>(TKey aId, string aAggregateType, AggregateChange<TKey> aChange)
            where TKey : struct, IEquatable<TKey>
        => new(
            aId,
            aChange.Content,
            aAggregateType,
            aChange.TransactionId,
            aChange.Version
        );
    }
}
