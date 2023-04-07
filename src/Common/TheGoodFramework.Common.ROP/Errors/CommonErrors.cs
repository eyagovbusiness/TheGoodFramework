using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Text;
using System.Threading.Tasks;

namespace TGF.Common.ROP.Errors
{
    public static class CommonErrors
    {
        public static class CancellationToken
        {
            public static HttpError Cancelled => new HttpError(
                new Error("CancellationToken.Cancelled",
                    "The request was cancelled."),
                HttpStatusCode.NoContent);

        }
    }
}
