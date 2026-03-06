using SkiaSharp;

namespace WorkHub.Services;

public class PhotoService
{
    private readonly ApiService _apiService;
    private const int MaxDimension = 1920;
    private const int JpegQuality = 80;

    public PhotoService(ApiService apiService)
    {
        _apiService = apiService;
    }

    public async Task<Models.PhotoResponse?> PickAndUploadCustomerPhotoAsync(Guid customerId)
    {
        var photo = await MediaPicker.PickPhotoAsync();
        if (photo == null) return null;
        return await CompressAndUploadAsync(photo, (stream, name) => _apiService.UploadCustomerPhotoAsync(customerId, stream, name));
    }

    public async Task<Models.PhotoResponse?> CaptureAndUploadCustomerPhotoAsync(Guid customerId)
    {
        var photo = await MediaPicker.CapturePhotoAsync();
        if (photo == null) return null;
        return await CompressAndUploadAsync(photo, (stream, name) => _apiService.UploadCustomerPhotoAsync(customerId, stream, name));
    }

    public async Task<Models.PhotoResponse?> PickAndUploadJobPhotoAsync(Guid jobId)
    {
        var photo = await MediaPicker.PickPhotoAsync();
        if (photo == null) return null;
        return await CompressAndUploadAsync(photo, (stream, name) => _apiService.UploadJobPhotoAsync(jobId, stream, name));
    }

    public async Task<Models.PhotoResponse?> CaptureAndUploadJobPhotoAsync(Guid jobId)
    {
        var photo = await MediaPicker.CapturePhotoAsync();
        if (photo == null) return null;
        return await CompressAndUploadAsync(photo, (stream, name) => _apiService.UploadJobPhotoAsync(jobId, stream, name));
    }

    public async Task<Models.PhotoResponse?> PickAndUploadProfilePhotoAsync()
    {
        var photo = await MediaPicker.PickPhotoAsync();
        if (photo == null) return null;
        return await CompressAndUploadAsync(photo, (stream, name) => _apiService.UploadProfilePhotoAsync(stream, name));
    }

    private async Task<Models.PhotoResponse?> CompressAndUploadAsync(FileResult photo, Func<Stream, string, Task<Models.PhotoResponse?>> uploadFunc)
    {
        using var sourceStream = await photo.OpenReadAsync();
        using var original = SKBitmap.Decode(sourceStream);
        if (original == null) return null;

        var (newWidth, newHeight) = CalculateSize(original.Width, original.Height);
        using var resized = original.Resize(new SKImageInfo(newWidth, newHeight), SKFilterQuality.Medium);
        using var image = SKImage.FromBitmap(resized ?? original);
        using var data = image.Encode(SKEncodedImageFormat.Jpeg, JpegQuality);
        using var compressedStream = new MemoryStream();
        data.SaveTo(compressedStream);
        compressedStream.Position = 0;

        var fileName = Path.ChangeExtension(photo.FileName, ".jpg");
        return await uploadFunc(compressedStream, fileName);
    }

    private static (int width, int height) CalculateSize(int originalWidth, int originalHeight)
    {
        if (originalWidth <= MaxDimension && originalHeight <= MaxDimension)
            return (originalWidth, originalHeight);

        var ratio = (double)originalWidth / originalHeight;
        if (originalWidth > originalHeight)
            return (MaxDimension, (int)(MaxDimension / ratio));
        return ((int)(MaxDimension * ratio), MaxDimension);
    }
}