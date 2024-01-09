using Microsoft.Extensions.DependencyInjection;

namespace TGF.Common.Serialization;

public static class SerializationDependencyInjection
{
    public static void AddSerializer(this IServiceCollection serviceCollection)
    {
        serviceCollection.AddTransient<ISerializer, Serializer>();
    }
}