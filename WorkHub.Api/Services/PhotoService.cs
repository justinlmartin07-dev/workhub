using Amazon.S3;
using Amazon.S3.Model;

namespace WorkHub.Api.Services;

public class PhotoService
{
    private readonly IAmazonS3 _s3;
    private readonly string _bucketName;

    public PhotoService(IAmazonS3 s3, IConfiguration config)
    {
        _s3 = s3;
        _bucketName = config["R2:BucketName"] ?? "workhub-photos";
    }

    public async Task<string> UploadAsync(string objectKey, Stream stream, string contentType)
    {
        var request = new PutObjectRequest
        {
            BucketName = _bucketName,
            Key = objectKey,
            InputStream = stream,
            ContentType = contentType,
        };

        await _s3.PutObjectAsync(request);
        return objectKey;
    }

    public async Task DeleteAsync(string objectKey)
    {
        await _s3.DeleteObjectAsync(_bucketName, objectKey);
    }

    public string GeneratePresignedUrl(string objectKey)
    {
        var request = new GetPreSignedUrlRequest
        {
            BucketName = _bucketName,
            Key = objectKey,
            Expires = DateTime.UtcNow.AddHours(1),
            Verb = HttpVerb.GET,
        };

        return _s3.GetPreSignedURL(request);
    }
}
