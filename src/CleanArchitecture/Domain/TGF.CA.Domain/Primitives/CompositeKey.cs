
namespace TGF.CA.Domain.Primitives
{
    public class CompositeKey<TKeys>(TKeys keyParts) where TKeys : IEquatable<TKeys>
    {
        public TKeys KeyParts { get; } = keyParts;

        public override bool Equals(object? obj)
        {
            return obj is CompositeKey<TKeys> other && KeyParts.Equals(other.KeyParts);
        }

        public override int GetHashCode()
        {
            return KeyParts.GetHashCode();
        }

        public static bool operator ==(CompositeKey<TKeys> left, CompositeKey<TKeys> right)
        {
            return Equals(left, right);
        }

        public static bool operator !=(CompositeKey<TKeys> left, CompositeKey<TKeys> right)
        {
            return !Equals(left, right);
        }
    }
}

