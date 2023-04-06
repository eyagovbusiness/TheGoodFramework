using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Text;
using System.Threading.Tasks;
using TGF.Common.ROP.Result;

namespace TGF.Common.ROP.HttpResult
{
    public interface IHttpResult<T>: IResult<T>
    {
        HttpStatusCode StatusCode { get; }
    }
}
