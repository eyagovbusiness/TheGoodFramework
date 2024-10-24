using Ardalis.Specification;
using TGF.CA.Application.Validation;
using TGF.Common.ROP.HttpResult;
using TGF.Common.ROP.Result;

namespace TGF.CA.Application.Specifications
{
    public class PagedSpecification<T>(int? page, int? pageSize, PaginationValidator paginationValidationRules) : ValidatedSpecification<T, PaginationValidator>(paginationValidationRules)
        where T : class
    {

        public int? Page { get; } = page;
        public int? PageSize { get; } = pageSize;

        public override IHttpResult<ISpecification<T>> Apply()
        => Result.ValidationResult(_validationRules.Validate(new PaginationValParams(Page, PageSize)))
        .Tap(_ => ApplyPagination(Query, Page, PageSize))
        .Map(_ => this as ISpecification<T>);

        internal static void ApplyPagination(ISpecificationBuilder<T> query, int? page, int? pageSize)
        {
            if (page != null && pageSize != null)
            {
                query.Skip((page!.Value - 1) * pageSize!.Value)
                .Take(pageSize.Value);
            }
        }

    }
}
