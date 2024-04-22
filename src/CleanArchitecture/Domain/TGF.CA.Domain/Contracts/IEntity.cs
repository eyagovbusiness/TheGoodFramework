namespace TGF.CA.Domain.Contracts
{
    public interface IEntity<TKey> where TKey : struct, IEquatable<TKey>
    {
        TKey Id { get; }

        bool Equals(object aOtherObject);
        int GetHashCode();
    }
}