
namespace TGF.CA.Domain.Contracts.Repositories
{
    /// <summary>
    /// Interface for repositories working with any class as entitiy type with the base default CRUD implementations.
    /// </summary>
    public interface IRepository<T> 
        : ICommandRepository<T>, IQueryRepository<T>
        where T : class
    {

    }
}
