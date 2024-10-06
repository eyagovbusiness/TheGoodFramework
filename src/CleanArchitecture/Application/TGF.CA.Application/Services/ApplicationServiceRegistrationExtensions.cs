using Microsoft.Extensions.DependencyInjection;
using System.Reflection;

namespace TGF.CA.Application.Services
{
    /// <summary>
    /// Provides extension methods for registering application services in the dependency injection container.
    /// </summary>
    public static class ApplicationServiceRegistrationExtensions
    {
        /// <summary>
        /// Registers all implementations of <see cref="IApplicationService"/> in the specified assembly with the dependency injection container.
        /// </summary>
        /// <param name="aServices">The <see cref="IServiceCollection"/> to add the services to.</param>
        /// <param name="aAssembly">The assembly to scan for application service implementations.</param>
        /// <returns>The <see cref="IServiceCollection"/> for chaining.</returns>
        /// <exception cref="InvalidOperationException">Thrown when an application service implementation cannot be registered.</exception>
        public static IServiceCollection AddApplicationServices(this IServiceCollection aServices, Assembly aAssembly)
        {
            // Cache the types from the assembly to avoid multiple enumeration
            var lAllTypes = aAssembly.GetTypes();

            // Find all types implementing IApplicationService
            var lServiceTypes = lAllTypes
                .Where(t => !t.IsAbstract && !t.IsInterface && typeof(IApplicationService).IsAssignableFrom(t))
                .Select(t => new { ImplementationType = t, InterfaceType = t.GetInterfaces().FirstOrDefault(i => i != typeof(IApplicationService) && typeof(IApplicationService).IsAssignableFrom(i)) });

            foreach (var lService in lServiceTypes)
            {
                if (lService.InterfaceType != null)
                {
                    try
                    {
                        aServices.AddScoped(lService.InterfaceType, lService.ImplementationType);
                    }
                    catch (Exception lEx)
                    {
                        // Handle or log the exception as needed
                        throw new InvalidOperationException($"Failed to register application service {lService.ImplementationType.Name} for interface {lService.InterfaceType.Name}", lEx);
                    }
                }
            }

            return aServices;
        }
    }
}
