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
        var photo = await PickPhotoSafeAsync();
        if (photo == null) return null;
        return await CompressAndUploadAsync(photo, (stream, name) => _apiService.UploadCustomerPhotoAsync(customerId, stream, name));
    }

    public async Task<Models.PhotoResponse?> CaptureAndUploadCustomerPhotoAsync(Guid customerId)
    {
        var photo = await CapturePhotoSafeAsync();
        if (photo == null) return null;
        return await CompressAndUploadAsync(photo, (stream, name) => _apiService.UploadCustomerPhotoAsync(customerId, stream, name));
    }

    public async Task<Models.PhotoResponse?> PickAndUploadJobPhotoAsync(Guid jobId)
    {
        var photo = await PickPhotoSafeAsync();
        if (photo == null) return null;
        return await CompressAndUploadAsync(photo, (stream, name) => _apiService.UploadJobPhotoAsync(jobId, stream, name));
    }

    public async Task<Models.PhotoResponse?> CaptureAndUploadJobPhotoAsync(Guid jobId)
    {
        var photo = await CapturePhotoSafeAsync();
        if (photo == null) return null;
        return await CompressAndUploadAsync(photo, (stream, name) => _apiService.UploadJobPhotoAsync(jobId, stream, name));
    }

    public async Task<Models.PhotoResponse?> PickAndUploadProfilePhotoAsync()
    {
        var photo = await PickPhotoSafeAsync();
        if (photo == null) return null;
        return await CompressAndUploadAsync(photo, (stream, name) => _apiService.UploadProfilePhotoAsync(stream, name));
    }

    private static async Task<FileResult?> CapturePhotoSafeAsync()
    {
        if (!MediaPicker.Default.IsCaptureSupported)
        {
            await Shell.Current.DisplayAlert("Camera", "No camera is available on this device.", "OK");
            return null;
        }
        try
        {
            return await MediaPicker.CapturePhotoAsync();
        }
        catch (PermissionException)
        {
            await Shell.Current.DisplayAlert("Camera", "Camera permission was denied. Enable it in Settings > Apps > WorkHub > Permissions.", "OK");
            return null;
        }
    }

    private static async Task<FileResult?> PickPhotoSafeAsync()
    {
        try
        {
            return await MediaPicker.PickPhotoAsync();
        }
        catch (PermissionException)
        {
            await Shell.Current.DisplayAlert("Photos", "Photo access was denied. Enable it in Settings > Apps > WorkHub > Permissions.", "OK");
            return null;
        }
    }

    private async Task<Models.PhotoResponse?> CompressAndUploadAsync(FileResult photo, Func<Stream, string, Task<Models.PhotoResponse?>> uploadFunc)
    {
        using var sourceStream = await photo.OpenReadAsync();
        using var buffered = new MemoryStream();
        await sourceStream.CopyToAsync(buffered);
        buffered.Position = 0;

        using var codec = SKCodec.Create(buffered);
        if (codec == null) return null;
        var decoded = SKBitmap.Decode(codec);
        if (decoded == null) return null;

        // Camera JPEGs carry rotation as an EXIF tag that SKBitmap.Decode ignores
        // and re-encoding strips, so bake the rotation into the pixels.
        using var original = ApplyExifOrientation(decoded, codec.EncodedOrigin);

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

    private static SKBitmap ApplyExifOrientation(SKBitmap bitmap, SKEncodedOrigin origin)
    {
        if (origin == SKEncodedOrigin.TopLeft) return bitmap;

        bool swapsAxes = origin is SKEncodedOrigin.LeftTop or SKEncodedOrigin.RightTop
            or SKEncodedOrigin.RightBottom or SKEncodedOrigin.LeftBottom;
        var upright = swapsAxes
            ? new SKBitmap(bitmap.Height, bitmap.Width)
            : new SKBitmap(bitmap.Width, bitmap.Height);

        using (var canvas = new SKCanvas(upright))
        {
            switch (origin)
            {
                case SKEncodedOrigin.TopRight:
                    canvas.Scale(-1, 1, bitmap.Width / 2f, 0);
                    break;
                case SKEncodedOrigin.BottomRight:
                    canvas.RotateDegrees(180, bitmap.Width / 2f, bitmap.Height / 2f);
                    break;
                case SKEncodedOrigin.BottomLeft:
                    canvas.Scale(1, -1, 0, bitmap.Height / 2f);
                    break;
                case SKEncodedOrigin.LeftTop:
                    canvas.Scale(-1, 1, upright.Width / 2f, 0);
                    canvas.Translate(upright.Width, 0);
                    canvas.RotateDegrees(90);
                    break;
                case SKEncodedOrigin.RightTop:
                    canvas.Translate(upright.Width, 0);
                    canvas.RotateDegrees(90);
                    break;
                case SKEncodedOrigin.RightBottom:
                    canvas.Scale(1, -1, 0, upright.Height / 2f);
                    canvas.Translate(upright.Width, 0);
                    canvas.RotateDegrees(90);
                    break;
                case SKEncodedOrigin.LeftBottom:
                    canvas.Translate(0, upright.Height);
                    canvas.RotateDegrees(-90);
                    break;
            }
            canvas.DrawBitmap(bitmap, 0, 0);
        }

        bitmap.Dispose();
        return upright;
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