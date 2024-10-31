using Ardalis.Specification;
using System.Collections.Concurrent;
using System.ComponentModel;
using System.Linq.Expressions;
using System.Reflection;
using TGF.CA.Application.Validation;
using TGF.Common.ROP.HttpResult;
using TGF.Common.ROP.HttpResult.RailwaySwitches;
using TGF.Common.ROP.Result;

namespace TGF.CA.Application.Specifications {

    /// <summary>
    /// Specification of T applying sorting logic on one of the T public properties
    /// </summary>
    /// <typeparam name="T">The type for the specification</typeparam>
    public class SortedSpecification<T>(
        string? sortBy,
        ListSortDirection? sortDirection,
        SortingValidator<T> sortingValidationRules) : ValidatedSpecification<T, SortingValidator<T>>(sortingValidationRules)
        where T : class
    {

        private static readonly ConcurrentDictionary<string, PropertyInfo?> _propertyCache = new();

        public string? SortBy { get; } = sortBy;
        public ListSortDirection? SortDirection { get; } = sortDirection;

        public override IHttpResult<ISpecification<T>> Apply()
        => Result.ValidationResult(_validationRules.Validate(new SortingValParams(SortBy, SortDirection)))
        .Tap(_ => ApplySorting(Query, SortBy, SortDirection))
        .Map(_ => this as ISpecification<T>);

        internal static void ApplySorting(ISpecificationBuilder<T> query, string? sortBy, ListSortDirection? sortDirection)
        {
            if (string.IsNullOrWhiteSpace(sortBy))
                return; // No sorting is applied if SortBy is not specified.

            // Get the cached PropertyInfo.
            var propertyInfo = GetCachedPropertyInfo(sortBy);

            // Create the expression to represent the property (x => x.Property).
            var parameter = Expression.Parameter(typeof(T), "x");
            var propertyExpression = Expression.Property(parameter, propertyInfo);
            var lambdaExpression = Expression.Lambda<Func<T, object?>>(
                Expression.Convert(propertyExpression, typeof(object)),
                parameter
            );

            // Apply sorting to the Query object.
            if (sortDirection != null && sortDirection.Value == ListSortDirection.Ascending)
            {
                query.OrderBy(lambdaExpression);
            }
            else
            {
                query.OrderByDescending(lambdaExpression);
            }
        }

        private static PropertyInfo GetCachedPropertyInfo(string? sortBy)
        {
            var cacheKey = $"{typeof(T).FullName}.{sortBy}";

            // Use the cache to avoid reflection overhead for property lookup.
            return _propertyCache.GetOrAdd(cacheKey, _ =>
            {
                var propertyInfo = typeof(T).GetProperty(sortBy!, BindingFlags.IgnoreCase | BindingFlags.Public | BindingFlags.Instance);
                return propertyInfo ?? throw new ArgumentException($"Property '{sortBy}' does not exist on type '{typeof(T).Name}'.");
            })!;
        }
    }
}
