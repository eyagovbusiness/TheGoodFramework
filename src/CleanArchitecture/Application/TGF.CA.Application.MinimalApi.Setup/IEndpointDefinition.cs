using Microsoft.AspNetCore.Builder;
using Microsoft.Extensions.DependencyInjection;

namespace TGF.CA.Application.MinimalApi.Setup
{
    /// <summary>
    /// Public interface that should be implemented in every encapsulation class that defines a set of minimal api endpoints.
    /// </summary>
    public interface IEndpointDefinition
    {
        void DefineRequiredServices(IServiceCollection aRequiredServicesCollection);
        void DefineEndpoints(WebApplication aWebApplication);
    }
}
