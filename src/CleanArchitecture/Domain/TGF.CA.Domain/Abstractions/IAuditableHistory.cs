namespace TGF.CA.Domain.Abstractions
{
    /// <summary>
    /// Represents the marker interface for auditable entities with basic time hisotry.
    /// </summary>
    public interface IAuditableHistory
    {
        DateTime? TOC { get; set; }
        DateTime? TOM { get; set; }
    }

    /// <summary>
    ///  Represents the marker interface for auditable entities with user hisotry and basic time hisotry.
    /// </summary>
    public interface IAuditableUserHistory<T> : IAuditableHistory
    {
        T UserIdM { get; set; }
        T UserIdC { get; set; }
    }

}
