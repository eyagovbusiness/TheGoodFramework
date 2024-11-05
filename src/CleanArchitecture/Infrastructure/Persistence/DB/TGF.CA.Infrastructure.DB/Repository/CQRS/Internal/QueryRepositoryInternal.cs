using Ardalis.Specification;
using Ardalis.Specification.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using TGF.Common.ROP.HttpResult;
using TGF.Common.ROP.Result;
using TGF.CA.Infrastructure.DB.Repository.CQRS.Base;
using TGF.CA.Domain.Contracts.Repositories;

namespace TGF.CA.Infrastructure.DB.Repository.CQRS.Internal {  
    /// <summary>
    /// A base class for a CQRS read only repository with native error handling logic for Query operations using ROP.
    /// </summary>
    /// <typeparam name="TRepository">The type of the child class implementing this repository.</typeparam>
    /// <typeparam name="TDbContext">The type of the DbContext to use in this repository.</typeparam>
    internal abstract class QueryRepositoryInternal<TRepository, TDbContext, T>(TDbContext aContext, ILogger<TRepository> aLogger, ISpecificationEvaluator specificationEvaluator)
    : QueryRepositoryBase<TRepository, TDbContext, T>(aContext, aLogger, specificationEvaluator), IQueryRepository<T>
    where TDbContext : Microsoft.EntityFrameworkCore.DbContext
    where TRepository : class
    where T : class
    {

        public QueryRepositoryInternal(TDbContext aContext, ILogger<TRepository> aLogger)
            : this(aContext, aLogger, SpecificationEvaluator.Default) {
        }

    }
}
