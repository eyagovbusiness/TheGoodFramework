using Ardalis.Specification;
using Ardalis.Specification.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using TGF.CA.Domain.Contracts.Repositories;
using TGF.CA.Infrastructure.DB.DbContext;
using TGF.CA.Infrastructure.DB.Repository.CQRS.Base;
using TGF.Common.ROP.HttpResult;
using TGF.Common.ROP.Result;

namespace TGF.CA.Infrastructure.DB.Repository.CQRS
{
    /// <summary>
    /// A base class for a CQRS read only repository with native error handling logic for Query operations using ROP.
    /// </summary>
    /// <typeparam name="TRepository">The type of the child class implementing this repository.</typeparam>
    /// <typeparam name="TDbContext">The type of the DbContext to use in this repository.</typeparam>
    public abstract class QueryRepository<TRepository, TDbContext, T>(TDbContext aContext, ILogger<TRepository> aLogger, ISpecificationEvaluator specificationEvaluator)
    : QueryRepositoryBase<TRepository, TDbContext, T>(aContext, aLogger, specificationEvaluator), IQueryRepository<T>
    where TDbContext : Microsoft.EntityFrameworkCore.DbContext, IReadOnlyDbContext
    where TRepository : class
    where T : class
    {

        public QueryRepository(TDbContext aContext, ILogger<TRepository> aLogger)
            : this(aContext, aLogger, SpecificationEvaluator.Default) {
        }

    }
}
