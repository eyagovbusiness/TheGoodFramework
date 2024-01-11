namespace TGF.CA.Domain
{
    /// <summary>
    /// Interface for auditable entities with basic time hisotry(created time and modified time).
    /// </summary>
    public interface IAuditableHistory
    {
        /// <summary>
        /// Creation date of this object.
        /// </summary>
        DateTime? TOC { get; set; }

        /// <summary>
        /// Last modification date of this object.
        /// </summary>
        DateTime? TOM { get; set; }

    }

    /// <summary>
    ///  Interface for auditable entities with user hisotry(user Id creator and last user Id who modified) and basic time hisotry(created time and modified time).
    /// </summary>
    public interface IAuditableUserHistory<TKey> : IAuditableHistory
        where TKey : struct, IEquatable<TKey>
    {
        /// <summary>
        /// Identifier of the user who created this object.
        /// </summary>
        TKey UserIdM { get; set; }

        /// <summary>
        /// Identifier of the user who made the last modification in this object.
        /// </summary>
        TKey UserIdC { get; set; }

        /// <summary>
        /// Set <see cref="UserIdC"/> and <see cref="IAuditableHistory.TOC"/>.
        /// </summary>
        /// <param name="aUserIdC">Id of the user who created this object.</param>
        void AuditCreation(TKey aUserIdC)
        {
            TOC = DateTime.Now;
            UserIdC = aUserIdC;
        }

        /// <summary>
        /// Set <see cref="UserIdM"/> and <see cref="IAuditableHistory.TOM"/>.
        /// </summary>
        /// <param name="aUserIdM">Id of the user who made the last modification of this object.</param>
        void AuditModification(TKey aUserIdM)
        {
            TOM = DateTime.UtcNow;
            UserIdM = aUserIdM;
        }

    }

}
