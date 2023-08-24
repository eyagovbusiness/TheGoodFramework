using System.Net;

namespace TGF.Common.ROP.Errors
{
    /// <summary>
    /// Static class that contains common <see cref="HttpError"/>s across applications.
    /// </summary>
    public static class CommonErrors
    {
        /// <summary>
        /// Static class that contains common <see cref="HttpError"/>s related with <see cref="System.Threading.CancellationToken"/>s across applications.
        /// </summary>
        public static class CancellationToken
        {
            /// <summary>
            /// Represents an <see cref="HttpError"/> that should be propagated when a <see cref="System.Threading.CancellationToken"/> is flagged to cancel the current operations.
            /// </summary>
            public static HttpError Cancelled => new(
                CancelledError,
                HttpStatusCode.RequestTimeout);

            /// <summary>
            /// Represents an <see cref="Error"/> that should be propagated when a <see cref="System.Threading.CancellationToken"/> is flagged to cancel the current operations.
            /// </summary>
            public static Error CancelledError => new(
                "CancellationToken.Cancelled",
                "The request was cancelled.");
        }
    }
}
