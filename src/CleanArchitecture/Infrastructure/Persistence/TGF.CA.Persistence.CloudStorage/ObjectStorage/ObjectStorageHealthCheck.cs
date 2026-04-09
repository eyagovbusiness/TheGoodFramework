using Microsoft.Extensions.Diagnostics.HealthChecks;

namespace TGF.CA.Infrastructure.Persistence.CloudStorage.ObjectStorage;

public class ObjectStorageHealthCheck(IObjectStorageProvider objectStorageProvider) : IHealthCheck {
    public async Task<HealthCheckResult> CheckHealthAsync(HealthCheckContext context, CancellationToken cancellationToken = default) {
        try {
            return await objectStorageProvider.CheckConnectionOrThrowAsync(cancellationToken)
                ? HealthCheckResult.Healthy($"Connection to {GetObjectStorageType()} established successfully.")
                : HealthCheckResult.Unhealthy($"Failed to connect to {GetObjectStorageType()}. Check connection string or network.");
        }
        catch (Exception ex) {
            return HealthCheckResult.Unhealthy($"($\"Failed to connect to {GetObjectStorageType()}. An exception occurred while trying to connect: {ex.Message}", ex);
        }
    }
    private string GetObjectStorageType()
        => objectStorageProvider is S3StorageProvider ? "AWS S3" : "Azure Blob Storage";

}   
