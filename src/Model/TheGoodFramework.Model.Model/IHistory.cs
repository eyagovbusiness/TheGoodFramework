namespace TheGoodFramework.Model
{
    /// <summary>
    /// Identify entities with basic time hisotry
    /// </summary>
    public interface IHistory
    {
        DateTime? TOC { get; set; }
        DateTime? TOM { get; set; }
    }

    /// <summary>
    /// Identify entities with user hisotry and basic time hisotry
    /// </summary>
    public interface IUserHistory : IHistory
    {
        int? UserIdM { get; set; }
        int? UserIdC { get; set; }
    }
}
