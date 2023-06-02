using Microsoft.Extensions.Diagnostics.HealthChecks;
using Newtonsoft.Json.Linq;
using TGF.CA.Infrastructure.Discovery;

namespace TGF.CA.Infrastructure.Communication.Health
{
    /// <summary>
    /// Abstract base class to perform health status communication between services. Gets the health of another service by callin that service's health endpoint.
    /// </summary>
    public abstract class ServiceHealthCheckBase : IHealthCheck
    {
        private readonly IHttpClientFactory _httpClientFactory;
        private readonly IServiceDiscovery _serviceDiscovery;
        private readonly string _serviceName;

        /// <summary>
        /// Base constructor(can't be invoked with "new" since the class is abstract and is not intended to exist by itself)
        /// </summary>
        /// <param name="aHttpClientFactory"></param>
        /// <param name="aDiscoveryService">Discovery or registry service used to get the required service address.</param>
        /// <param name="aServiceName">Service name that represents the ServiceKey of the required service was registered in the DiscoveryService.</param>
        public ServiceHealthCheckBase(IHttpClientFactory aHttpClientFactory, IServiceDiscovery aDiscoveryService, string aServiceName)
        {
            _httpClientFactory = aHttpClientFactory;
            _serviceDiscovery = aDiscoveryService;
            _serviceName = aServiceName;
        }

        /// <summary>
        /// Abstracted implementation of the health checking communication. Override and call it in in the child class.
        /// </summary>
        /// <param name="aContext"></param>
        /// <param name="aCancellationToken"></param>
        /// <returns></returns>
        public virtual async Task<HealthCheckResult> CheckHealthAsync(HealthCheckContext aContext, CancellationToken aCancellationToken = default)
        {
            HttpClient lHttpClient = _httpClientFactory.CreateClient();
            string lMicroserviceHealthEndpoint = await _serviceDiscovery.GetFullAddress(_serviceName, aCancellationToken);
            lHttpClient.BaseAddress = new Uri(lMicroserviceHealthEndpoint);
            HttpResponseMessage lHttpResponseMessage = await lHttpClient.GetAsync("health", aCancellationToken);

            string lContent = await lHttpResponseMessage.Content.ReadAsStringAsync();
            // Parse the health status from the JSON content
            var lStatusString = JObject.Parse(lContent)["status"]?.ToString();
            lHttpClient.Dispose();

            return lStatusString switch
            {
                "Healthy" => HealthCheckResult.Healthy("The service is healthy."),
                "Degraded" => HealthCheckResult.Degraded("The service is degraded."),
                _ => HealthCheckResult.Unhealthy("The service is down.")
            };

        }

    }
}
