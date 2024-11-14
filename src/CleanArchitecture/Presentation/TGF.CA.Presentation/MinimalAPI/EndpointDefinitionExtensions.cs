using Microsoft.AspNetCore.Builder;
using Microsoft.Extensions.DependencyInjection;

namespace TGF.CA.Presentation.MinimalAPI
{
    /// <summary>
    /// Extensions class used to help on registering all the defined endpoints by scanning assemblies loking for classes that implement <see cref="IEndpointDefinition"/>.
    /// </summary>
    public static class EndpointDefinitionExtensions
    {
        /// <summary>
        /// Adds as singleton services all the classes that implement <see cref="IEndpointDefinition"/> after calling <see cref="IEndpointDefinition.DefineRequiredServices"/> for each one. This does register the requiered services by the endpoints defined in each specific <see cref="IEndpointDefinition"/>
        /// </summary>
        /// <param name="aServiceCollection"></param>
        /// <param name="aScanMarkerList"></param>
        public static void AddEndpointDefinitions(this IServiceCollection aServiceCollection, params Type[] aScanMarkerList)
        {
            var lEndpointDefinitionList = new List<IEndpointsDefinition>();
            foreach (var lMarker in aScanMarkerList)
            {
                lEndpointDefinitionList.AddRange(
                    lMarker.Assembly.ExportedTypes
                        .Where(x => typeof(IEndpointsDefinition).IsAssignableFrom(x) && !x.IsAbstract && !x.IsInterface)
                        .Select(Activator.CreateInstance).Cast<IEndpointsDefinition>()
                );
            }
            foreach (var lEndpointDefinition in lEndpointDefinitionList)
                lEndpointDefinition.DefineRequiredServices(aServiceCollection);
            aServiceCollection.AddSingleton(lEndpointDefinitionList as IReadOnlyCollection<IEndpointsDefinition>);
        }

        /// <summary>
        /// Defines in our already built <see cref="WebApplication"/> the endpoints declared in every <see cref="IEndpointDefinition"/> that was registered in the <see cref="WebApplicationBuilder"/> from <see cref="AddEndpointDefinitions(IServiceCollection, Type[])"/>
        /// </summary>
        /// <param name="aWebApplication"></param>
        public static void UseEndpointDefinitions(this WebApplication aWebApplication)
        {
            var lEndpointDefinitionList = aWebApplication.Services.GetRequiredService<IReadOnlyCollection<IEndpointsDefinition>>();
            foreach (var lEndpointDefinition in lEndpointDefinitionList)
                lEndpointDefinition.DefineEndpoints(aWebApplication);
        }

    }
}
