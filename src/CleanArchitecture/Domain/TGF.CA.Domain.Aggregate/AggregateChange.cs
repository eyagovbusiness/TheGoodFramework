using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace TGF.CA.Domain.Aggregate
{
    public record AggregateChange(object Content, Guid Id, Type Type, string TransactionId, int Version, bool IsNew);

    //Stored in DB
    public class AggregateChangeEntitiy
    {
        public object Content { get; set; }
        public Guid AggregateId { get; set; }

        public string AggregateType { get; set; }
        public string TransactionId { get; set; }
        public int AggregateVersion { get; set; }

        public AggregateChangeEntitiy(object content, Guid aggregateId, string aggregateType, string transactionId, int aggregateVersion)
        {
            Content = content;
            AggregateId = aggregateId;
            AggregateType = aggregateType;
            TransactionId = transactionId;
            AggregateVersion = aggregateVersion;
        }
    }
}
