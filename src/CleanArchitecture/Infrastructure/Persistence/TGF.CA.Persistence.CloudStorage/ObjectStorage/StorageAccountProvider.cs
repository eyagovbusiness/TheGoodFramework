using Azure.Storage.Blobs;
using Azure.Storage.Sas;
using Microsoft.Extensions.Configuration;
using TGF.CA.Infrastructure.InvariantConstants;
using TGF.CA.Infrastructure.Secrets;
using TGF.CA.Infrastructure.Secrets.SecretsFiles;

namespace TGF.CA.Infrastructure.Persistence.CloudStorage.ObjectStorage;

public class StorageAccountProvider(IConfiguration configuration, ISecretFilesService secretFilesService) : IObjectStorageProvider {
    private readonly Lazy<Task<string>> _connectionString = new(() => GetStorageAccountConnectionString(configuration, secretFilesService));
    private BlobServiceClient? _blobServiceClient;

    #region IObjectStorageProvider Implementation
    public async Task<bool> CheckConnectionAsync(CancellationToken cancellationToken = default) {
        try {
            var client = await GetBlobServiceClientAsync();
            await client.GetPropertiesAsync(cancellationToken);
            return true;
        }
        catch {
            return false;
        }
    }

    public async Task<Stream> GetObjectAsync(string containerName, string objectName, CancellationToken cancellationToken = default) {
        var client = await GetBlobServiceClientAsync();
        var blobClient = client.GetBlobContainerClient(containerName).GetBlobClient(objectName);
        var response = await blobClient.DownloadAsync(cancellationToken);
        return response.Value.Content;
    }

    public async Task DeleteObjectAsync(string containerName, string objectName, CancellationToken cancellationToken = default) {
        var client = await GetBlobServiceClientAsync();
        var blobClient = client.GetBlobContainerClient(containerName).GetBlobClient(objectName);
        await blobClient.DeleteIfExistsAsync(cancellationToken: cancellationToken);
    }

    public async Task<IEnumerable<string>> ListContainersAsync(CancellationToken cancellationToken = default) {
        var client = await GetBlobServiceClientAsync();
        var containers = new List<string>();
        await foreach (var containerItem in client.GetBlobContainersAsync(cancellationToken: cancellationToken)) {
            containers.Add(containerItem.Name);
        }
        return containers;
    }

    public async Task CreateContainerAsync(string containerName, CancellationToken cancellationToken = default) {
        var client = await GetBlobServiceClientAsync();
        await client.CreateBlobContainerAsync(containerName, cancellationToken: cancellationToken);
    }
    public async Task PutObjectAsync(string containerName, string objectName, Stream data, string? contentType = null, IDictionary<string, string>? metadata = null, CancellationToken cancellationToken = default) {
        var client = await GetBlobServiceClientAsync();
        var blobClient = client.GetBlobContainerClient(containerName).GetBlobClient(objectName);
        await blobClient.UploadAsync(data, overwrite: true, cancellationToken);
        if (metadata != null) {
            await blobClient.SetMetadataAsync(metadata, cancellationToken: cancellationToken);
        }
    }

    public async Task<Uri?> GetContainerAccessUriAsync(string containerName, TimeSpan expiry, CancellationToken cancellationToken = default) {
        var client = await GetBlobServiceClientAsync();
        var containerClient = client.GetBlobContainerClient(containerName);
        var sasBuilder = new BlobSasBuilder {
            BlobContainerName = containerName,
            Resource = "c",
            ExpiresOn = DateTimeOffset.UtcNow.Add(expiry)
        };
        sasBuilder.SetPermissions(BlobContainerSasPermissions.Read | BlobContainerSasPermissions.List);
        return containerClient.GenerateSasUri(sasBuilder);
    }
    #endregion

    #region Public StorageAccountProvider Methods
    public async Task<BlobServiceClient> GetBlobServiceClientAsync() {
        if (_blobServiceClient == null) {
            var connStr = await _connectionString.Value;
            _blobServiceClient = new BlobServiceClient(connStr);
        }
        return _blobServiceClient;
    }

    public async Task<BlobContainerClient> GetBlobContainerClientAsync(string containerName)
        => (await GetBlobServiceClientAsync()).GetBlobContainerClient(containerName);

    public async Task<BlobClient> GetBlobClientAsync(string containerName, string blobName) {
        var containerClient = (await GetBlobServiceClientAsync()).GetBlobContainerClient(containerName);
        return containerClient.GetBlobClient(blobName);
    }

    #endregion

    #region Private Static Methods
    private static async Task<string> GetStorageAccountConnectionString(IConfiguration configuration1, ISecretFilesService secretFilesService) {
        var storageAccountSecretsSourceType = configuration1.GetValue<string>(ConfigurationKeys.ObjectStorage.SecretsSourceType);

        Enum.TryParse(typeof(SecretsSourceTypeEnum), storageAccountSecretsSourceType, false, out var secretsSourceType);
        return secretsSourceType switch {
            SecretsSourceTypeEnum.File => await secretFilesService.GetSecretFromConfigAsync(ConfigurationKeys.SecretsFiles.SecretsFileNames.ObjectStorageConnectionString),
            _ => throw new NotSupportedException("[ERROR]: The provided value in appsettings of SecretsSourceType in CloudStorage section is not a supported secrets source")
        };
    }
    #endregion

}

