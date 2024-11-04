
namespace TGF.CA.Domain.Contracts.Repositories.Base
{
    /// <summary>
    /// Interface with the TryQuery and TryComand methods working with any class as entities.
    /// </summary>
    public interface IRepositoryBase<T> 
        : ICommandRepositoryBase<T>, IQueryRepositoryBase<T>
        where T : class
    {

    }
}
