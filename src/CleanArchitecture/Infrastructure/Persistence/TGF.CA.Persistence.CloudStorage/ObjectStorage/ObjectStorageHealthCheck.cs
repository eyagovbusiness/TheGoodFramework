using Microsoft.Extensions.Diagnostics.HealthChecks;

namespace TGF.CA.Infrastructure.Persistence.CloudStorage.ObjectStorage;

public class ObjectStorageHealthCheck(IObjectStorageProvider objectStorageProvider) : IHealthCheck {
    public async Task<HealthCheckResult> CheckHealthAsync(HealthCheckContext context, CancellationToken cancellationToken = default) 
    =>  await objectStorageProvider.CheckConnectionAsync(cancellationToken)
        ? HealthCheckResult.Healthy($"Connection to {GetObjectStorageType()} established successfully.")
        : HealthCheckResult.Unhealthy($"Failed to connect to {GetObjectStorageType()}. Check connection string or network.");

    private string GetObjectStorageType()
        => objectStorageProvider is S3StorageProvider ? "AWS S3" : "Azure Blob Storage";

}   
