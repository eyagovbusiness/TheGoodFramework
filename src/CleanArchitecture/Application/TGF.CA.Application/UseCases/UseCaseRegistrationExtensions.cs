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
        public static IServiceCollection AddUseCases(this IServiceCollection services, Assembly assembly)
        {
            // Cache the types from the assembly to avoid multiple enumeration
            var allTypes = assembly.GetTypes();

            // Find all types implementing IUseCase<TResponse, TRequest>
            var useCaseTypes = allTypes
                .Where(t => !t.IsAbstract && !t.IsInterface)
                .SelectMany(t => t.GetInterfaces()
                    .Where(i => i.IsGenericType && i.GetGenericTypeDefinition() == typeof(IUseCase<,>))
                    .Select(i => new { InterfaceType = i, ImplementationType = t }));

            foreach (var useCase in useCaseTypes)
            {
                try
                {
                    // Register the service by interface and by concrete type
                    services.AddScoped(useCase.InterfaceType, useCase.ImplementationType);
                    services.AddScoped(useCase.ImplementationType); // Register by concrete type as well since otherwise there could be no multiple use case with the same input and output types in the generic interface registered in DI.
                }
                catch (Exception ex)
                {
                    // Handle or log the exception as needed
                    throw new InvalidOperationException($"Failed to register use case {useCase.ImplementationType.Name}", ex);
                }
            }

            return services;
        }

    }
}
