using Microsoft.Extensions.Logging;
using TGF.CA.Domain.Contracts;
using TGF.CA.Infrastructure.DB.Repository.CQRS.Internal;

namespace TGF.CA.Infrastructure.DB.Repository.CQRS.EntityRepository
{
    /// <summary>
    /// A base class for a CQRS write repository with native error handling logic for Command operations using ROP.
    /// </summary>
    /// <typeparam name="TRepository">The type of the child class implementing this repository.</typeparam>
    /// <typeparam name="TDbContext">The type of the DbContext to use in this repository.</typeparam>
    public abstract class EntityCommandRepository<TRepository, TDbContext, T, TKey>(TDbContext aContext, ILogger<TRepository> aLogger)
        : CommandRepository<TRepository, TDbContext, T>(aContext, aLogger), IEntityCommandRepositoryInternal<T, TKey>
        where TDbContext : Microsoft.EntityFrameworkCore.DbContext
        where TRepository : class
        where T : class, IEntity<TKey>
        where TKey : IEquatable<TKey>
    {

    }
}
