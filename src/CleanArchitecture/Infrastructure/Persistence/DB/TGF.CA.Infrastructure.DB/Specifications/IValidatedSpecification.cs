using Ardalis.Specification;
using TGF.Common.ROP.HttpResult;

namespace TGF.CA.Infrastructure.DB.Specifications
{
    public interface IValidatedSpecification<T, TSpecValidator>
        where TSpecValidator : FluentValidation.IValidator
    {
        IHttpResult<ISpecification<T>> Apply();
    }
}
