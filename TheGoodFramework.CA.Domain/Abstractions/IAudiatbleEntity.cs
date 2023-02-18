namespace TheGoodFramework.CA.Domain.Abstractions
{
    /// <summary>
    /// Represents the marker interface for auditable entities.
    /// </summary>
    public interface IAuditableEntity
    {
        /// <summary>
        /// Gets the date and time of creation in UTC format.
        /// </summary>
        DateTime TOC { get; }

        /// <summary>
        /// Gets the date and time of the last modification in UTC format.
        /// </summary>
        DateTime? TOM { get; }
    }
}
