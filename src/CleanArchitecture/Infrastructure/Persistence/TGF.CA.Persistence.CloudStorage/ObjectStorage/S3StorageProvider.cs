using Amazon.S3;
using Amazon.S3.Model;

namespace TGF.CA.Infrastructure.Persistence.CloudStorage.ObjectStorage;

public class S3StorageProvider(IAmazonS3 s3Client) : IObjectStorageProvider {

    #region IObjectStorageProvider Implementation
    public async Task<bool> CheckConnectionOrThrowAsync(CancellationToken cancellationToken = default) {
        try {
            await s3Client.HeadBucketAsync(new HeadBucketRequest { //Requires s3:HeadBucket permission
                BucketName = Environment.GetEnvironmentVariable("AWS_BUCKET_NAME") ?? throw new InvalidOperationException("S3_BUCKET_NAME environment variable is not set.")
            }, cancellationToken);

            return true; // Success means connection is valid
        }
        catch {
            throw;
        }

    }

    public async Task<Stream> GetObjectAsync(string bucketName, string objectName, CancellationToken cancellationToken = default) {
        var response = await s3Client.GetObjectAsync(bucketName, objectName, cancellationToken);
        return response.ResponseStream;
    }

    public async Task PutObjectAsync(string bucketName, string objectName, Stream data, string? contentType = null, IDictionary<string, string>? metadata = null, CancellationToken cancellationToken = default) {
        var request = new PutObjectRequest {
            BucketName = bucketName,
            Key = objectName,
            InputStream = data,
            ContentType = contentType ?? "application/octet-stream"
        };

        if (metadata != null) {
            foreach (var kvp in metadata) {
                request.Metadata[kvp.Key] = kvp.Value;
            }
        }

        await s3Client.PutObjectAsync(request, cancellationToken);
    }

    public async Task DeleteObjectAsync(string bucketName, string objectName, CancellationToken cancellationToken = default) {
        await s3Client.DeleteObjectAsync(bucketName, objectName, cancellationToken);
    }

    public async Task<IEnumerable<string>> ListContainersAsync(CancellationToken cancellationToken = default) {
        var response = await s3Client.ListBucketsAsync(cancellationToken);
        return response.Buckets.Select(b => b.BucketName);
    }

    public async Task CreateContainerAsync(string bucketName, CancellationToken cancellationToken = default) {
        await s3Client.PutBucketAsync(bucketName, cancellationToken);
    }

    public Task<Uri?> GetContainerAccessUriAsync(string bucketName, TimeSpan expiry, CancellationToken cancellationToken = default) {
        var request = new GetPreSignedUrlRequest {
            BucketName = bucketName,
            Expires = DateTime.UtcNow.Add(expiry),
            Verb = HttpVerb.GET
        };
        var url = s3Client.GetPreSignedURL(request);
        return Task.FromResult<Uri?>(new Uri(url));
    }
    #endregion

}

