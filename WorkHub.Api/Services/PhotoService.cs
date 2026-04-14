using Amazon.S3;
using Amazon.S3.Model;

namespace WorkHub.Api.Services;

public class PhotoService
{
    private readonly IAmazonS3 _s3;
    private readonly string _bucketName;
    private readonly ILogger<PhotoService> _logger;

    public PhotoService(IAmazonS3 s3, IConfiguration config, ILogger<PhotoService> logger)
    {
        _s3 = s3;
        _bucketName = config["R2_BUCKET_NAME"] ?? config["R2:BucketName"] ?? "workhub-photos";
        _logger = logger;
    }

    public async Task<string> UploadAsync(string objectKey, Stream stream, string contentType)
    {
        try
        {
            var request = new PutObjectRequest
            {
                BucketName = _bucketName,
                Key = objectKey,
                InputStream = stream,
                ContentType = contentType,
                DisablePayloadSigning = true,
            };

            await _s3.PutObjectAsync(request);
            return objectKey;
        }
        catch (AmazonS3Exception ex)
        {
            _logger.LogError(ex, "R2 upload failed for {ObjectKey} (bucket={Bucket}, status={Status}, code={Code})",
                objectKey, _bucketName, ex.StatusCode, ex.ErrorCode);
            throw;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Unexpected error uploading {ObjectKey} to R2", objectKey);
            throw;
        }
    }

    public async Task DeleteAsync(string objectKey)
    {
        try
        {
            await _s3.DeleteObjectAsync(_bucketName, objectKey);
        }
        catch (AmazonS3Exception ex)
        {
            _logger.LogError(ex, "R2 delete failed for {ObjectKey} (bucket={Bucket}, status={Status}, code={Code})",
                objectKey, _bucketName, ex.StatusCode, ex.ErrorCode);
            throw;
        }
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
