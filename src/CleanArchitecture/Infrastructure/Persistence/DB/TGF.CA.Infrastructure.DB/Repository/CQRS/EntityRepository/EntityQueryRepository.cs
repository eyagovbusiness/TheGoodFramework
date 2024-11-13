﻿using Ardalis.Specification;
using Ardalis.Specification.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using TGF.CA.Domain.Contracts.Repositories.EntityRepository;
using TGF.CA.Infrastructure.DB.DbContext;
using TGF.Common.ROP.HttpResult;
using TGF.Common.ROP.Result;

namespace TGF.CA.Infrastructure.DB.Repository.CQRS.EntityRepository
{
    /// <summary>
    /// A base class for a CQRS read only repository with native error handling logic for Query operations using ROP.
    /// </summary>
    /// <typeparam name="TRepository">The type of the child class implementing this repository.</typeparam>
    /// <typeparam name="TDbContext">The type of the DbContext to use in this repository.</typeparam>
    public abstract class EntityQueryRepository<TRepository, TDbContext, T, TKey>(TDbContext aContext, ILogger<TRepository> aLogger, ISpecificationEvaluator specificationEvaluator)
    : QueryRepository<TRepository, TDbContext, T>(aContext, aLogger, specificationEvaluator), IEntityQueryRepository<T, TKey>
    where TDbContext : Microsoft.EntityFrameworkCore.DbContext, IReadOnlyDbContext
    where TRepository : class
    where T : class, Domain.Contracts.IEntity<TKey>
    where TKey : IEquatable<TKey>
    {

        public EntityQueryRepository(TDbContext aContext, ILogger<TRepository> aLogger)
            : this(aContext, aLogger, SpecificationEvaluator.Default)
        {
        }


        #region Read

        public virtual async Task<IHttpResult<T>> GetByIdAsync(TKey entityId, CancellationToken cancellationToken = default)
        => await TryQueryAsync(async cancellationToken =>
        {
            var entity = await Queryable.FirstOrDefaultAsync(e => e.Id.Equals(entityId), cancellationToken);
            return entity != null ? Result.SuccessHttp(entity!) : Result.Failure<T>(DBErrors.Repository.Entity.NotFound);
        }, cancellationToken);

        public virtual async Task<IHttpResult<IEnumerable<T>>> GetByIdListAsync(IEnumerable<TKey> entityIds, CancellationToken cancellationToken = default)
        => await TryQueryAsync(async cancellationToken =>
        {
            var entityIdList = entityIds.ToList();
            if (entityIdList.Count == 0) return Result.SuccessHttp(Enumerable.Empty<T>());

            var entities = await Queryable.Where(e => entityIdList.Contains(e.Id)).ToListAsync(cancellationToken);
            return entities.Count != 0 ? Result.SuccessHttp(entities as IEnumerable<T>) : Result.Failure<IEnumerable<T>>(DBErrors.Repository.Entity.NotFound);
        }, cancellationToken);

        #endregion

    }
}