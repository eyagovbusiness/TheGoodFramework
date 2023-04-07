using System.Collections.Immutable;
using TGF.Common.ROP.Errors;

namespace TGF.Common.ROP.Result
{
    /// <summary>
    /// Public interface for any <see cref="Result{T}"/>.
    /// </summary>
    public interface IResult<T>
    {
        T Value { get; }
        bool IsSuccess { get; }
        ImmutableArray<IError> ErrorList { get; }
    }
}
