using Microsoft.AspNetCore.Builder;
using Microsoft.Extensions.DependencyInjection;

namespace TGF.CA.Presentation.MinimalAPI {
    /// <summary>
    /// Public interface that should be implemented in every encapsulation class that defines a set of minimal api endpoints.
    /// </summary>
    public interface IEndpointsDefinition {
        /// <summary>
        /// Define here the endpoint definitions
        /// </summary>
        void DefineEndpoints(WebApplication aWebApplication);

        /// <summary>
        /// Optional to implement, here all services required(specific) by any of the decred endpoint definitons in this instance can be added to the DI container.
        /// </summary>
        void DefineRequiredServices(IServiceCollection aRequiredServicesCollection) {

        }
    }
}
