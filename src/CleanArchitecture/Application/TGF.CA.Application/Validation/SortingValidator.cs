using FluentValidation;
using System.Reflection;
using System.ComponentModel;
using TGF.CA.Application.Errors.Validation;
using TGF.Common.ROP.Errors;

namespace TGF.CA.Application.Validation
{
    public abstract class SortingValidator<T> : AbstractValidator<SortingValParams>
        where T : class
    {
        public SortingValidator()
        {
            RuleFor(x => x.SortBy)
                .NotEmpty().WithErrorCode(TGFApplicationErrors.Validation.Sorting.SortByEmpty_Code)
                .Must(BeAValidProperty).WithROPError(TGFApplicationErrors.Validation.Sorting.SortByInvalid)
                .When(x => !string.IsNullOrWhiteSpace(x.SortBy) && x.SortDirection.HasValue);

            RuleFor(x => x)
            .Must(x =>
                x.SortBy != null && x.SortDirection.HasValue ||
                x.SortBy == null && !x.SortDirection.HasValue)
            .WithROPError(TGFApplicationErrors.Validation.Sorting.InconsistentParameters);
        }

        private bool BeAValidProperty(string? aPropName)
        {
            if (string.IsNullOrEmpty(aPropName))
                return false;
            var propertyNames = typeof(T).GetProperties(BindingFlags.Public | BindingFlags.Instance)
                .Select(prop => prop.Name.ToLowerInvariant());
            return propertyNames.Contains(aPropName.ToLowerInvariant());
        }
    }

    public record SortingValParams(string? SortBy, ListSortDirection? SortDirection);
}
