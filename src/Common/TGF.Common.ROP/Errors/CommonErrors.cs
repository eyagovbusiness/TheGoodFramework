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

        public static class ContextAccessToken
        {
            /// <summary>
            /// Represents an <see cref="HttpError"/> that should be propagated when the access token in the request's http context is null or empty.
            /// </summary>
            public static HttpError NullOrEmpty => new(
                NullOrEmptyError,
                HttpStatusCode.BadRequest);

            /// <summary>
            /// Represents an <see cref="Error"/> that should be propagated when the access token in the request's http context is null or empty.
            /// </summary>
            public static Error NullOrEmptyError => new(
                "ContextAccessToken.NullOrEmpty",
                "The access token in the request's http context is null or empty.");
        }

        public static class UnhandledException
        {
            public static HttpError Cancelled => new(
            new Error("CancellationToken.Cancelled",
                "The request was cancelled."),
            HttpStatusCode.InternalServerError);
            public static HttpError New(string aExceptionMessage, HttpStatusCode aStatusCode = HttpStatusCode.InternalServerError)
            {
                return new HttpError(
                    new Error("UnhandledException", aExceptionMessage),
                    aStatusCode);
            }
        }

    }
}
