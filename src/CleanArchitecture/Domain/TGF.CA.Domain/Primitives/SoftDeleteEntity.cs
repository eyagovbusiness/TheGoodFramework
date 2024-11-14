
namespace TGF.CA.Domain.Primitives
{
    /// <summary>
    /// Represents a base class for entities that support soft deletion.
    /// Inherits from <see cref="Entity{TKey}"/> to leverage the generic key handling and
    /// introduces a flag for soft deletion.
    /// </summary>
    /// <typeparam name="TKey">
    /// The type of the primary key for the entity. TKey must be a value type
    /// (struct) and implement IEquatable<TKey> for efficient equality comparison.
    /// Examples of valid types include int, long, Guid, etc.
    /// </typeparam>
    public abstract class SoftDeleteEntity<TKey> 
        : Entity<TKey>, ISoftDelete
        where TKey : IEquatable<TKey>
    {
        public bool IsDeleted { get; private set; }

        protected SoftDeleteEntity() { }

        public void SoftDelete()
        => IsDeleted = true;

        public void RestoreSoftDelete()
        => IsDeleted = false;

    }
}
