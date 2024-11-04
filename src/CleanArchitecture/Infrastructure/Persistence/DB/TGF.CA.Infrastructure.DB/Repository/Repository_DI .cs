using Microsoft.Extensions.DependencyInjection;
using System.Reflection;
using TGF.CA.Domain.Contracts.Repositories;

namespace TGF.CA.Infrastructure.DB.Repository
{
    public static class Repository_DI
    {
        /// <summary>
        /// Injects in DI container all classes in the assembly implementing any of the repository interfaces in "TGF.CA.Domain.Contracts.Repositories" />.
        /// </summary>
        public static void AddRepositories(this IServiceCollection services, Assembly assembly)
        {
            var repositoryTypes = new[] 
            { 
                typeof(IEntitiyRepository<,>),
                typeof(IEntitiyCommandRepository<,>),
                typeof(IEntityQueryRepository<,>),
                typeof(IRepository<>),
                typeof(ICommandRepository<>),
                typeof(IQueryRepository<>) 
            };
            var types = assembly.GetTypes();
            foreach (var type in types)
            {
                var interfaces = type.GetInterfaces();
                foreach (var @interface in interfaces)
                {
                    if (@interface.IsGenericType && repositoryTypes.Any(repositoryType => @interface.GetGenericTypeDefinition() == repositoryType))
                    {
                        var typeInterface = interfaces
                            .FirstOrDefault(typeInterface => typeInterface.Name.Contains(type.Name))
                                ?? throw new Exception($"[SF.Manager.Infrastructure][ERROR] Failed attempt to register the {type.Name} repository: it does not implement a I{type.Name} interface and this was expected, please add the interface to the repository.");
                        services.AddScoped(typeInterface, type);
                    }
                }
            }
        }
    }
}
