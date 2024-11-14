using Ardalis.Specification;
using System.ComponentModel;
using TGF.CA.Application.Validation;
using TGF.Common.ROP.HttpResult;
using TGF.Common.ROP.HttpResult.RailwaySwitches;
using TGF.Common.ROP.Result;

namespace TGF.CA.Application.Specifications {
    public class SortedAndPagedSpecification<T>(
        int? page, int? pageSize,
        string? sortBy, ListSortDirection? sortDirection,
        PaginationValidator paginationValidationRules, SortingValidator<T> sortingValidationRules)
    : ValidatedSpecification<T, PaginationValidator>(paginationValidationRules)
        where T : class
    {

        public int? Page { get; } = page;
        public int? PageSize { get; } = pageSize;

        public string? SortBy { get; } = sortBy;
        public ListSortDirection? SortDirection { get; } = sortDirection;

        public override IHttpResult<ISpecification<T>> Apply()
        => Result.ValidationResult(sortingValidationRules.Validate(new SortingValParams(SortBy, SortDirection)))
        .Tap(_ => SortedSpecification<T>.ApplySorting(Query, SortBy, SortDirection))
        .Bind(_ => Result.ValidationResult(_validationRules.Validate(new PaginationValParams(Page, PageSize))))
        .Tap(_ => PagedSpecification<T>.ApplyPagination(Query, Page, PageSize))
        .Map(_ => this as ISpecification<T>);

    }
}
