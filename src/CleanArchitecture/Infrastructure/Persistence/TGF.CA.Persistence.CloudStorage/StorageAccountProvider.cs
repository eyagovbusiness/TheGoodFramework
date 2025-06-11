using Azure.Storage.Blobs;
using Azure.Storage.Files.Shares;
using Azure.Storage.Files.Shares.Models;
using Microsoft.Extensions.Configuration;
using TGF.CA.Infrastructure.InvariantConstants;
using TGF.CA.Infrastructure.Secrets;
using TGF.CA.Infrastructure.Secrets.SecretsFiles;

namespace TGF.CA.Infrastructure.Persistence.CloudStorage {
    public class StorageAccountProvider(IConfiguration configuration, ISecretFilesService secretFilesService) : ICloudStorageProvider {
        private readonly Lazy<Task<string>> _storageAccountConnectionString = new(() => GetStorageAccountConnectionString(configuration, secretFilesService));
        private BlobServiceClient? _blobServiceClient;
        private ShareServiceClient? _shareServiceClient;

        public async Task<string> GetAccountNameAsync() {
            if (_blobServiceClient == null) {
                var connectionString = await _storageAccountConnectionString.Value;
                _blobServiceClient = new BlobServiceClient(connectionString);
            }
            return _blobServiceClient.AccountName;
        }

        public async Task<BlobClient> GetBlobClientAsync(string containerName, string blobName) {
            if (_blobServiceClient == null) {
                var connectionString = await _storageAccountConnectionString.Value;
                _blobServiceClient = new BlobServiceClient(connectionString);
            }
            var containerClient = _blobServiceClient.GetBlobContainerClient(containerName);
            return containerClient.GetBlobClient(blobName);
        }
        public async Task<BlobContainerClient> GetBlobContainerClientAsync(string containerName) {
            if (_blobServiceClient == null) {
                var connectionString = await _storageAccountConnectionString.Value;
                _blobServiceClient = new BlobServiceClient(connectionString);
            }
            return _blobServiceClient.GetBlobContainerClient(containerName);
        }

        public async Task<ShareClient> GetShareClientAsync(string shareName) {
            if (_shareServiceClient == null) {
                var connectionString = await _storageAccountConnectionString.Value;
                _shareServiceClient = new ShareServiceClient(connectionString);
            }
            return _shareServiceClient.GetShareClient(shareName);
        }

        public async Task<IEnumerable<ShareClient>> GetAllShareClientsAsync(CancellationToken cancellationToken = default) {
            if (_shareServiceClient == null) {
                var connectionString = await _storageAccountConnectionString.Value;
                _shareServiceClient = new ShareServiceClient(connectionString);
            }

            var shareClients = new List<ShareClient>();
            await foreach (var shareItem in _shareServiceClient.GetSharesAsync(ShareTraits.Metadata, cancellationToken: cancellationToken)) {
                shareClients.Add(_shareServiceClient.GetShareClient(shareItem.Name));
            }

            return shareClients;
        }

        private static async Task<string> GetStorageAccountConnectionString(IConfiguration configuration1, ISecretFilesService secretFilesService) {
            var storageAccountSecretsSourceType = configuration1.GetValue<string>(ConfigurationKeys.CloudStorage.SecretsSourceType);

            Enum.TryParse(typeof(SecretsSourceTypeEnum), storageAccountSecretsSourceType, false, out var secretsSourceType);
            return secretsSourceType switch {
                SecretsSourceTypeEnum.File => await secretFilesService.GetSecretFromConfigAsync(ConfigurationKeys.SecretsFiles.SecretsFileNames.CloudStorageConnectionString),
                _ => throw new NotSupportedException("[ERROR]: The provided value in appsettings of SecretsSourceType in CloudStorage section is not a supported secrets source")
            };
        }

    }
}
