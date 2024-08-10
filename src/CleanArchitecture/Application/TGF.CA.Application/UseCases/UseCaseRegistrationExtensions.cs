using Microsoft.Extensions.DependencyInjection;
using System.Reflection;

namespace TGF.CA.Application.UseCases
{
    /// <summary>
    /// Provides extension methods for registering use cases in the dependency injection container.
    /// </summary>
    public static class UseCaseRegistrationExtensions
    {
        /// <summary>
        /// Registers all implementations of <see cref="IUseCase{TResponse, TRequest}"/> in the specified assembly with the dependency injection container.
        /// </summary>
        /// <param name="aServices">The <see cref="IServiceCollection"/> to add the services to.</param>
        /// <param name="aAssembly">The assembly to scan for use case implementations.</param>
        /// <returns>The <see cref="IServiceCollection"/> for chaining.</returns>
        /// <exception cref="InvalidOperationException">Thrown when a use case implementation cannot be registered.</exception>
        public static IServiceCollection AddUseCases(this IServiceCollection aServices, Assembly aAssembly)
        {
            // Cache the types from the assembly to avoid multiple enumeration
            var lAllTypes = aAssembly.GetTypes();

            // Find all types implementing IUseCase<TResponse, TRequest>
            var lUseCaseTypes = lAllTypes
                .Where(t => !t.IsAbstract && !t.IsInterface)
                .SelectMany(t => t.GetInterfaces()
                    .Where(i => i.IsGenericType && i.GetGenericTypeDefinition() == typeof(IUseCase<,>))
                    .Select(i => new { ImplementationType = t, InterfaceType = i }));

            foreach (var lUseCase in lUseCaseTypes)
            {
                try
                {
                    aServices.AddScoped(lUseCase.InterfaceType, lUseCase.ImplementationType);
                }
                catch (Exception lEx)
                {
                    // Handle or log the exception as needed
                    throw new InvalidOperationException($"Failed to register use case {lUseCase.ImplementationType.Name} for interface {lUseCase.InterfaceType.Name}", lEx);
                }
            }

            return aServices;
        }
    }
}
