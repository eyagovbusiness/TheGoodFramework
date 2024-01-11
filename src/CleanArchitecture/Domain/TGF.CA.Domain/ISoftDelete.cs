namespace TGF.CA.Domain
{
    /// <summary>
    /// Interface for soft deletable entities.
    /// </summary>
    public interface ISoftDelete
    {
        /// <summary>
        /// Indicates whether this Entity has been marked as deleted.
        /// This is the flag used for soft deletion.
        /// </summary>
        bool IsDeleted { get; }

        /// <summary>
        /// Marks the entity as deleted. This is the method to call to soft delete an entity.
        /// </summary>
        void SoftDelete();

        /// <summary>
        /// Restores the entity from a soft deleted state.
        /// This method can be used to undo a soft delete.
        /// </summary>
        void RestoreSoftDelete();

    }

}
