using Microsoft.AspNetCore.Http;

namespace TGF.CA.Infrastructure
{
    public class CaptureIpMiddleware(RequestDelegate aNext)
    {
        public async Task InvokeAsync(HttpContext aContext)
        {
            var lIpAddress = aContext.Connection.RemoteIpAddress?.ToString();
            if (!string.IsNullOrEmpty(lIpAddress))
                aContext.Items["ClientIpAddress"] = lIpAddress;

            await aNext(aContext);
        }
    }

}
