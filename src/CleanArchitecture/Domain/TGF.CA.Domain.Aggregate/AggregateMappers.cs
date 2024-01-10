
namespace TGF.CA.Domain.Aggregate
{
    public static class AggregateMappers
    {
        public static AggregateChange ToAggregateChange(AggregateChangeEntity aChange)
        =>new(
            aChange.Content,
            aChange.AggregateId,
            aChange.GetType(),
            aChange.TransactionId,
            aChange.AggregateVersion,
            false
        );

        public static AggregateChangeEntity ToTypedAggregateChangeDto(Guid aId, string aAggregateType, AggregateChange aChange)
        => new(
            aId,
            aChange.Content,
            aAggregateType,
            aChange.TransactionId,
            aChange.Version
        );
    }
}
