namespace TGF.CA.Domain.Contracts
{
    public interface IEntity<TKey> where TKey : IEquatable<TKey>
    {
        /// <summary>
        /// The unique identifier for the Entity.
        /// </summary>
        TKey Id { get; }

        /// <summary>
        /// When the entity was created in DB
        /// </summary>
        public DateTimeOffset CreatedAt { get; set; }
        /// <summary>
        /// The last time when the entity was modified, by default it is initially set to <see cref="CreatedAt"/> when the entity is created.
        /// </summary>
        public DateTimeOffset ModifiedAt { get; set; }

        bool Equals(object aOtherObject);
        int GetHashCode();
    }
}