using Amazon;
using Amazon.Runtime;
using Amazon.S3;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using TGF.CA.Infrastructure.InvariantConstants;
using TGF.CA.Infrastructure.Persistence.CloudStorage.ObjectStorage;

namespace TGF.CA.Infrastructure.Persistence.CloudStorage;

public static class ObjectStorage_DI {
    /// <summary>
    /// Configures the application's object storage provider based on the current hosting environment.
    /// </summary>
    /// <remarks>This method registers an implementation of IObjectStorageProvider appropriate for the
    /// detected environment (Azure, AWS, or Docker in development). It also adds a health check for the object storage
    /// provider. Ensure that the environment is correctly set to avoid unsupported configuration errors.</remarks>
    /// <param name="webApplicationBuilder">The WebApplicationBuilder instance to configure with cloud storage services.</param>
    /// <param name="webHostEnvironment">The current hosting environment, used to determine which cloud storage provider to register.</param>
    /// <exception cref="NotSupportedException">Thrown if the hosting environment is not supported or if Docker is used outside of development.</exception>
    public static void AddCloudStorage(this WebApplicationBuilder webApplicationBuilder, IWebHostEnvironment webHostEnvironment) {
        _ = webHostEnvironment.GetHostEnvironment() switch {
            HostEnvironmentEnum.Azure => webApplicationBuilder.Services.AddSingleton<IObjectStorageProvider, StorageAccountProvider>(),
            HostEnvironmentEnum.AWS => webApplicationBuilder.AddS3RequiredServices(),
            HostEnvironmentEnum.Docker => webHostEnvironment.IsDevelopment()
                ? webApplicationBuilder.Services.AddSingleton<IObjectStorageProvider, StorageAccountProvider>()
                : throw new NotSupportedException("[ERROR]: Docker only supports cloud storage in development."),
            _ => throw new NotSupportedException("[ERROR]: Unsupported cloud provider.")
        };

        webApplicationBuilder.Services.AddHealthChecks().AddCheck<ObjectStorageHealthCheck>(InfrastrcutureConstants.HealthCheckNames.ObjectStorage);
    }

    private static IServiceCollection AddS3RequiredServices(this WebApplicationBuilder webApplicationBuilder) {
        //TODO-BEGIN: Remove all this and use secret file based configuration instead like StorageAccountProvider 
        var awsOptions = webApplicationBuilder.Configuration.GetAWSOptions();
        webApplicationBuilder.Services.AddDefaultAWSOptions(awsOptions);
        webApplicationBuilder.Services.AddSingleton<IAmazonS3>(sp => {
            var credentials = new EnvironmentVariablesAWSCredentials();
            var regionEnv = Environment.GetEnvironmentVariable(EnvironmentVariableNames.AWS_REGION);
            var region = !string.IsNullOrEmpty(regionEnv) ? RegionEndpoint.GetBySystemName(regionEnv) : RegionEndpoint.EUNorth1;
            return new AmazonS3Client(credentials, region);
        });
        //TODO-END

        return webApplicationBuilder.Services.AddSingleton<IObjectStorageProvider, S3StorageProvider>();
    }

}

