using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.DependencyInjection;
namespace TGF.CA.Infrastructure.Persistence.CloudStorage {
    public static class CloudStorage_DI {
        public static IServiceCollection AddCloudStorage(this IServiceCollection services, IWebHostEnvironment webHostEnvironment)
        => webHostEnvironment.GetHostEnvironment() switch {
            HostEnvironmentEnum.Azure => services.AddSingleton<ICloudStorageProvider, StorageAccountProvider>(),
            HostEnvironmentEnum.Docker => webHostEnvironment.IsDevelopment() ? services.AddSingleton<ICloudStorageProvider, StorageAccountProvider>() : throw new NotSupportedException("[ERROR]: The ASPNETCORE_ENVIRONMENT environment variable is set to docker but only _dev environment supports cloud storage"),
            _ => throw new NotSupportedException("[ERROR]: The specified cloud provider specified in ASPNETCORE_ENVIRONMENT environment variable is not supported.")
        };
    }
}
