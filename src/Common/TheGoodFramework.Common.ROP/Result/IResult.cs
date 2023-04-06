using System.Collections.Immutable;
using TGF.Common.ROP.Errors;

namespace TGF.Common.ROP.Result
{
    public interface IResult<T>
    {
        T Value { get; }
        bool IsSuccess { get; }
        ImmutableArray<IError> ErrorList { get; }
    }
}
