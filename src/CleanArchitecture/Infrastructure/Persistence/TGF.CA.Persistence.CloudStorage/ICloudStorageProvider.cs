using Azure.Storage.Blobs;
using Azure.Storage.Files.Shares;

namespace TGF.CA.Infrastructure.Persistence.CloudStorage {
    public interface ICloudStorageProvider {
        Task<string> GetAccountNameAsync();
        Task<BlobClient> GetBlobClientAsync(string containerName, string blobName);
        Task<BlobContainerClient> GetBlobContainerClientAsync(string containerName);
        Task<ShareClient> GetShareClientAsync(string shareName);
        Task<IEnumerable<ShareClient>> GetAllShareClientsAsync(CancellationToken cancellationToken);
        }
    }