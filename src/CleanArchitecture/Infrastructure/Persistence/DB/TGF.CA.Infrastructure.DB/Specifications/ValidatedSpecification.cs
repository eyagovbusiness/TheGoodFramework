using Ardalis.Specification;
using TGF.Common.ROP.HttpResult;

namespace TGF.CA.Infrastructure.DB.Specifications
{
    public abstract class ValidatedSpecification<T, TSpecValidator>(TSpecValidator validationRules)
        : Specification<T>, IValidatedSpecification<T, TSpecValidator>
        where TSpecValidator : FluentValidation.IValidator
    {

        protected readonly TSpecValidator _validationRules = validationRules;

        public abstract IHttpResult<ISpecification<T>> Apply();

    }
}
