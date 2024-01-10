using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace TGF.CA.Domain.Aggregate
{
    public static class AggregateMappers
    {
        public static AggregateChange ToAggregateChange(AggregateChangeEntitiy change)
        {
            return new AggregateChange(
                change.Content,
                change.AggregateId,
                change.GetType(),
                change.TransactionId,
                change.AggregateVersion,
                false
            );
        }

        public static AggregateChangeEntitiy ToTypedAggregateChangeDto(
            Guid Id,
            string aggregateType,
            AggregateChange change
        )
        {
            return new AggregateChangeEntitiy(
                change.Content,
                Id,
                aggregateType,
                change.TransactionId,
                change.Version
            );
        }
    }
}
