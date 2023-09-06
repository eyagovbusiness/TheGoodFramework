using Microsoft.AspNetCore.Http;
using System.Net;
using TGF.CA.Application;

namespace TGF.CA.Infrastructure.Middleware.Security
{
    /// <summary>
    /// Represents the base middleware for API key verification. This service verifies every request with a URL containing the "/private/" segment also has a header with the right API key of the targeted microservice API.
    /// </summary>
    public abstract class ApiKeyMiddlewareBase
    {
        private readonly string PrivatePathSegment = "/private/";
        private readonly RequestDelegate _next;
        private readonly ISecretsManager _secretsManager;
        private readonly Lazy<Task<string>> _serviceAPIKey;

        /// <summary>
        /// Gets the name of the service which is used to derive the API key header name.
        /// </summary>
        protected readonly string _serviceName;

        /// <summary>
        /// Gets the API key header name based on the service name.
        /// </summary>
        private string ServiceAPIKeyName => _serviceName + "-api-key";

        /// <summary>
        /// Initializes a new instance of the <see cref="ApiKeyMiddlewareBase"/> class.
        /// </summary>
        /// <param name="serviceName">The name of the service.</param>
        /// <param name="next">The next middleware in the pipeline.</param>
        /// <param name="secretsManager">The secrets manager to retrieve the API key.</param>
        /// <exception cref="ArgumentNullException">Thrown when serviceName is null or empty.</exception>
        public ApiKeyMiddlewareBase(string serviceName, RequestDelegate next, ISecretsManager secretsManager)
        {
            if (string.IsNullOrEmpty(serviceName))
                throw new ArgumentNullException(nameof(serviceName));

            _serviceName = serviceName;
            _next = next;
            _secretsManager = secretsManager ?? throw new ArgumentNullException(nameof(secretsManager));
            _serviceAPIKey = new Lazy<Task<string>>(() => _secretsManager.GetServiceKey(_serviceName));
        }

        /// <summary>
        /// Invokes the middleware for request processing.
        /// </summary>
        /// <param name="context">The current HTTP context.</param>
        /// <returns>A <see cref="Task"/> representing the asynchronous operation.</returns>
        public async Task InvokeAsync(HttpContext context)
        {
            if (context.Request.Path.Value?.Contains(PrivatePathSegment, StringComparison.OrdinalIgnoreCase) == true)
            {
                var lServiceAPIKeyValue = await _serviceAPIKey.Value;
                if (string.IsNullOrEmpty(lServiceAPIKeyValue))
                    throw new InvalidOperationException($"The API key could not be retrieved for the {_serviceName} service.");

                if (!context.Request.Headers.TryGetValue(ServiceAPIKeyName, out var apiKeyHeaderValue)
                    || string.IsNullOrEmpty(apiKeyHeaderValue)
                    || apiKeyHeaderValue != lServiceAPIKeyValue)
                {
                    context.Response.StatusCode = (int)HttpStatusCode.Forbidden;
                    await context.Response.WriteAsync("Invalid service's API Key.");
                    return;
                }
            }
            await _next.Invoke(context);
        }

    }

    // Example for derived classes:
    //public class ApiKeyMiddleware : ApiKeyMiddlewareBase
    //{
    //    public DerivedMiddleware(RequestDelegate next, ISecretsManager secretsManager)
    //        : base("ProductsService", next, secretsManager)
    //    {
    //    }
    //}

}
