using Amazon.S3;
using Amazon.S3.Model;
using AdvancedRag.App.DocumentImages;

namespace AdvancedRag.Infrastructure.DocumentImages;

public sealed class S3DocumentImageObjectStorage : IDocumentImageObjectStorage
{
    private readonly IAmazonS3 _s3;
    private readonly string _bucketName;

    public S3DocumentImageObjectStorage(IAmazonS3 s3, string bucketName)
    {
        _s3 = s3;
        _bucketName = bucketName;
    }

    public async Task PutAsync(string objectKey, string contentType, Stream content, CancellationToken ct)
    {
        PutObjectRequest request = new()
        {
            BucketName = _bucketName,
            Key = objectKey,
            InputStream = content,
            ContentType = contentType,
            AutoCloseStream = false,
        };
        await _s3.PutObjectAsync(request, ct);
    }

    public async Task<Stream> GetAsync(string objectKey, CancellationToken ct)
    {
        GetObjectResponse response = await _s3.GetObjectAsync(_bucketName, objectKey, ct);
        MemoryStream buffer = new();
        await response.ResponseStream.CopyToAsync(buffer, ct);
        buffer.Position = 0;
        response.Dispose();
        return buffer;
    }
}
