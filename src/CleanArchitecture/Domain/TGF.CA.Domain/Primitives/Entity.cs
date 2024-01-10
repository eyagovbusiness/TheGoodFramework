
namespace TGF.CA.Domain.Primitives
{
    /// <summary>
    /// Represents the base class for all entities in the domain model.
    /// This class introduces a generic approach to handling entity keys,
    /// allowing for flexibility in the choice of key type while ensuring
    /// certain type constraints are met.
    /// </summary>
    /// <typeparam name="TKey">
    /// The type of the primary key for the entity. TKey must be a value type
    /// (struct) and implement IEquatable<TKey> for efficient equality comparison.
    /// Examples of valid types include int, long, Guid, etc.
    /// </typeparam>
    public abstract class Entity<TKey>
        where TKey : struct, IEquatable<TKey>
    {
        /// <summary>
        /// The unique identifier for the Entity.
        /// </summary>
        public TKey Id { get; protected set; }

        protected Entity() { }

        /// <summary>
        /// Determines whether the specified object is equal to the current object.
        /// Equality is based solely on the entity's identifier.
        /// </summary>
        /// <param name="aObject">The object to compare with the current object.</param>
        /// <returns>true if the specified object is equal to the current object; otherwise, false.</returns>
#pragma warning disable CS8765 // Nullability of type of parameter doesn't match overridden member (possibly because of nullability attributes).
        public override bool Equals(object aOtherObject)
#pragma warning restore CS8765 // Nullability of type of parameter doesn't match overridden member (possibly because of nullability attributes).
        => aOtherObject is Entity<TKey> lOtherObject && Id.Equals(lOtherObject.Id);


        /// <summary>
        /// Serves as the default hash function.
        /// The hash code is based on the entity's identifier.
        /// </summary>
        /// <returns>A hash code for the current object.</returns>
        public override int GetHashCode()
        => Id.GetHashCode();

        /// <summary>
        /// Determines whether two specified instances of Entity<TKey> are equal.
        /// </summary>
        /// <param name="aFirstEntity">The first object to compare.</param>
        /// <param name="aSecondEntity">The second object to compare.</param>
        /// <returns>true if <see cref="aFirstEntity"/> and <see cref="aSecondEntity"/> represent the same object or both are null; otherwise, false.</returns>
        public static bool operator ==(Entity<TKey> aFirstEntity, Entity<TKey> aSecondEntity)
        {
            if (ReferenceEquals(aFirstEntity, aSecondEntity))
                return true;

            if (ReferenceEquals(aFirstEntity, null) || ReferenceEquals(aSecondEntity, null))
                return false;

            return aFirstEntity.Equals(aSecondEntity);
        }

        /// <summary>
        /// Determines whether two specified instances of Entity<TKey> are not equal.
        /// </summary>
        /// <param name="aFirstEntity">The first object to compare.</param>
        /// <param name="aSecondEntity">The second object to compare.</param>
        /// <returns>true if a and b do not represent the same object; otherwise, false.</returns>
        public static bool operator !=(Entity<TKey> aFirstEntity, Entity<TKey> aSecondEntity)
        => !(aFirstEntity == aSecondEntity);

    }
}
