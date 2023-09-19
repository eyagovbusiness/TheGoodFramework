using Microsoft.Extensions.DependencyInjection;

namespace TGF.Common.Serialization;
//CODE FROM https://github.com/ElectNewt/Distribt
public static class SerializationDependencyInjection
{
    public static void AddSerializer(this IServiceCollection serviceCollection)
    {
        serviceCollection.AddTransient<ISerializer, Serializer>();
    }
}