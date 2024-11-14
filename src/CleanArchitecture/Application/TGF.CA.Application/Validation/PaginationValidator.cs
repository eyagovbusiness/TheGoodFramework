using FluentValidation;
using TGF.Common.ROP.Errors;
using TGF.CA.Application.Errors.Validation;

namespace TGF.CA.Application.Validation
{
    public class PaginationValidator : AbstractValidator<PaginationValParams>
    {
        public PaginationValidator()
        {
            // Custom rule to ensure either both are specified or neither must be specified
            RuleFor(x => x)
                .Must(x =>
                    x.Page.HasValue && x.PageSize.HasValue ||
                    !x.Page.HasValue && !x.PageSize.HasValue)
                .WithROPError(TGFApplicationErrors.Validation.Pagination.InconsistentParameters);

            // Validate that Page is greater than 0 if specified
            RuleFor(x => x.Page)
                .GreaterThan(0)
                .When(x => x.Page.HasValue && x.PageSize.HasValue)
                .WithErrorCode(TGFApplicationErrors.Validation.Pagination.Page_Code);

            // Validate that PageSize is greater than 0 if specified
            RuleFor(x => x.PageSize)
                .GreaterThan(0)
                .When(x => x.Page.HasValue && x.PageSize.HasValue)
                .WithErrorCode(TGFApplicationErrors.Validation.Pagination.PageSize_Code);
        }
    }

    public record PaginationValParams(int? Page, int? PageSize);
}
