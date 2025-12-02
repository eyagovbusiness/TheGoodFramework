
namespace TGF.CA.Infrastructure.Persistence.CloudStorage.ObjectStorage;

/// <summary>
/// Defines a provider-agnostic abstraction for interacting with object storage services
/// such as Azure Blob Storage or AWS S3.
/// </summary>
/// <remarks>
/// This interface provides methods for common operations like uploading, downloading,
/// deleting objects, managing containers/buckets, and generating access URIs.
/// Implementations should handle provider-specific details internally.
/// </remarks>
public interface IObjectStorageProvider {
    /// <summary>
    /// Checks whether a connection to the object storage service can be established making a lightweight call to verify connectivity
    /// </summary>
    /// <param name="cancellationToken">Token to cancel the operation.</param>
    /// <returns>
    /// A task that returns <c>true</c> if the connection is successful; otherwise <c>false</c>.
    /// </returns>
    Task<bool> CheckConnectionAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// Downloads an object from the specified container or bucket.
    /// </summary>
    /// <param name="containerName">The name of the container or bucket.</param>
    /// <param name="objectName">The name of the object to retrieve.</param>
    /// <param name="cancellationToken">Token to cancel the operation.</param>
    /// <returns>A task that returns a stream containing the object's data.</returns>
    Task<Stream> GetObjectAsync(string containerName, string objectName, CancellationToken cancellationToken = default);

    /// <summary>
    /// Uploads an object to the specified container or bucket.
    /// </summary>
    /// <param name="containerName">The name of the container or bucket.</param>
    /// <param name="objectName">The name of the object to upload.</param>
    /// <param name="data">The stream containing the object's data.</param>
    /// <param name="contentType">Optional content type of the object.</param>
    /// <param name="metadata">Optional metadata to associate with the object.</param>
    /// <param name="cancellationToken">Token to cancel the operation.</param>
    Task PutObjectAsync(string containerName, string objectName, Stream data, string? contentType = null, IDictionary<string, string>? metadata = null, CancellationToken cancellationToken = default);

    /// <summary>
    /// Deletes an object from the specified container or bucket.
    /// </summary>
    /// <param name="containerName">The name of the container or bucket.</param>
    /// <param name="objectName">The name of the object to delete.</param>
    /// <param name="cancellationToken">Token to cancel the operation.</param>
    Task DeleteObjectAsync(string containerName, string objectName, CancellationToken cancellationToken = default);

    /// <summary>
    /// Lists all containers or buckets available in the storage account.
    /// </summary>
    /// <param name="cancellationToken">Token to cancel the operation.</param>
    /// <returns>A task that returns a collection of container or bucket names.</returns>
    Task<IEnumerable<string>> ListContainersAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// Creates a new container or bucket in the storage account.
    /// </summary>
    /// <param name="containerName">The name of the container or bucket to create.</param>
    /// <param name="cancellationToken">Token to cancel the operation.</param>
    Task CreateContainerAsync(string containerName, CancellationToken cancellationToken = default);

    /// <summary>
    /// Generates an access URI for the specified container or bucket.
    /// </summary>
    /// <remarks>
    /// For Azure, this returns a SAS URI; for AWS, a pre-signed URL.
    /// </remarks>
    /// <param name="containerName">The name of the container or bucket.</param>
    /// <param name="expiry">The duration for which the URI should remain valid.</param>
    /// <param name="cancellationToken">Token to cancel the operation.</param>
    /// <returns>A task that returns the access URI, or <c>null</c> if not supported.</returns>
    Task<Uri?> GetContainerAccessUriAsync(string containerName, TimeSpan expiry, CancellationToken cancellationToken = default);
}



