
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
    public abstract class SoftDeleteEntity<TKey> : Entity<TKey>
        where TKey : struct, IEquatable<TKey>
    {
        /// <summary>
        /// Gets or sets a value indicating whether this entity has been marked as deleted.
        /// This is the flag used for soft deletion.
        /// </summary>
        public bool IsDeleted { get; private set; }

        /// <summary>
        /// Initializes a new instance of the SoftDeleteEntity class.
        /// </summary>
        protected SoftDeleteEntity() { }

        /// <summary>
        /// Initializes a new instance of the SoftDeleteEntity class with a specified identifier.
        /// </summary>
        /// <param name="id">The unique identifier for the entity.</param>
        protected SoftDeleteEntity(TKey id) : base(id) { }

        /// <summary>
        /// Marks the entity as deleted. This is the method to call to soft delete an entity.
        /// </summary>
        public void MarkAsDeleted()
        => IsDeleted = true;

        /// <summary>
        /// Restores the entity from a soft deleted state.
        /// This method can be used to undo a soft delete.
        /// </summary>
        public void Restore()
        => IsDeleted = false;

    }
}
