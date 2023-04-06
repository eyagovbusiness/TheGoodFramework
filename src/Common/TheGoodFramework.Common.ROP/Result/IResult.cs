using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;
using System.Net;
using System.Text;
using System.Threading.Tasks;
using TGF.Common.ROP.Errors;
using TGF.Common.ROP.HttpResult;

namespace TGF.Common.ROP.Result
{
    public interface IResult<T>
    {
        T Value { get; }
        bool IsSuccess { get; }
        ImmutableArray<IError> ErrorList { get; }
    }
}
