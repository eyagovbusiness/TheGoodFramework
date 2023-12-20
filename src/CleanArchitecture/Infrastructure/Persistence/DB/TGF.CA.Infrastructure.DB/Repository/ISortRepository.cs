using System.Collections.Concurrent;
using System.Linq.Expressions;

namespace TGF.CA.Infrastructure.DB.Repository
{
    public interface ISortRepository
    {
        protected static readonly ConcurrentDictionary<string, LambdaExpression> _sortExpressions = new ConcurrentDictionary<string, LambdaExpression>();

        /// <summary>
        /// Applies sorting to an <see cref="IQueryable"/> based on a specified property.
        /// </summary>
        /// <typeparam name="T">The type of the entity in the <see cref="IQueryable"/>.</typeparam>
        /// <param name="aQuery">The IQueryable to apply the sorting to.</param>
        /// <param name="aSortBy">The property name to sort by.</param>
        /// <param name="aSortDirection">The sorting direction(ascending/descending).</param>
        /// <returns>An <see cref="IQueryable"/> sorted based on the specified property.</returns>
        /// <remarks>
        /// This method uses a static ConcurrentDictionary to cache the sorting expressions,
        /// improving performance by avoiding repeated reflection. The method is thread-safe
        /// and suitable for use in environments with concurrent requests. If the property name
        /// is not found, or if it is null or whitespace, the original <see cref="IQueryable"/> is returned
        /// unmodified. This method only supports ascending order sorting.
        /// </remarks>
        protected static IQueryable<T> ApplySorting<T>(IQueryable<T> aQuery, string aSortBy, SortDirection aSortDirection = SortDirection.Ascending)
        {
            if (string.IsNullOrWhiteSpace(aSortBy))
                return aQuery;

            var lCacheKey = $"{typeof(T).FullName}.{aSortBy}.{aSortDirection}";
            var lLambda = _sortExpressions.GetOrAdd(lCacheKey, _ =>
            {
                var lPropertyInfo = typeof(T).GetProperty(aSortBy);
                if (lPropertyInfo == null)
                    return null!;

                var parameter = Expression.Parameter(typeof(T), "x");
                var property = Expression.Property(parameter, lPropertyInfo);
                return Expression.Lambda(property, parameter);
            });

            if (lLambda == null)
                return aQuery;

            string methodName = aSortDirection == SortDirection.Ascending ? "OrderBy" : "OrderByDescending";
            var lOrderByCallExpression = Expression.Call(
                typeof(Queryable),
                methodName,
                [typeof(T), lLambda.Body.Type],
                aQuery.Expression,
                Expression.Quote(lLambda));

            return aQuery.Provider.CreateQuery<T>(lOrderByCallExpression);
        }
        protected enum SortDirection
        {
            Ascending,
            Descending
        }
    }
}
