using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace TGF.CA.Domain.Primitives
{
    public abstract class CompositeEntity<TKeys> : EntityBase
        where TKeys : IEquatable<TKeys>
    {
        public CompositeKey<TKeys> Id { get; protected set; }

        protected CompositeEntity(TKeys keyParts)
        {
            Id = new CompositeKey<TKeys>(keyParts);
            CreatedAt = DateTimeOffset.UtcNow;
            ModifiedAt = CreatedAt;
        }

        public override bool Equals(object obj)
        {
            return obj is CompositeEntity<TKeys> other && Id.Equals(other.Id);
        }

        public override int GetHashCode()
        {
            return Id.GetHashCode();
        }

        public static bool operator ==(CompositeEntity<TKeys> left, CompositeEntity<TKeys> right)
        {
            return Equals(left, right);
        }

        public static bool operator !=(CompositeEntity<TKeys> left, CompositeEntity<TKeys> right)
        {
            return !Equals(left, right);
        }
    }
}

