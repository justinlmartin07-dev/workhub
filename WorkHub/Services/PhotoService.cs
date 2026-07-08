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

    public async Task<List<Models.PhotoResponse>> PickAndUploadMultipleJobPhotosAsync(Guid jobId, Action<int, int>? onProgress = null)
    {
        var photos = await PickMultiplePhotosSafeAsync();
        return await UploadMultipleAsync(photos, (stream, name) => _apiService.UploadJobPhotoAsync(jobId, stream, name), onProgress);
    }

    public async Task<List<Models.PhotoResponse>> PickAndUploadMultipleCustomerPhotosAsync(Guid customerId, Action<int, int>? onProgress = null)
    {
        var photos = await PickMultiplePhotosSafeAsync();
        return await UploadMultipleAsync(photos, (stream, name) => _apiService.UploadCustomerPhotoAsync(customerId, stream, name), onProgress);
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

#if ANDROID
    internal const int MultiPickRequestCode = 9876;
    private static TaskCompletionSource<IReadOnlyList<Android.Net.Uri>>? _multiPickTcs;

    // Called by MainActivity.OnActivityResult.
    internal static void HandleMultiPickResult(Android.App.Result resultCode, Android.Content.Intent? data)
    {
        var tcs = System.Threading.Interlocked.Exchange(ref _multiPickTcs, null);
        if (tcs == null) return;

        if (resultCode != Android.App.Result.Ok || data == null)
        {
            tcs.TrySetResult([]);
            return;
        }

        var uris = new List<Android.Net.Uri>();
        if (data.ClipData != null)
        {
            for (int i = 0; i < data.ClipData.ItemCount; i++)
            {
                var uri = data.ClipData.GetItemAt(i)?.Uri;
                if (uri != null) uris.Add(uri);
            }
        }
        else if (data.Data != null)
        {
            uris.Add(data.Data);
        }
        tcs.TrySetResult(uris);
    }

    private static async Task<IReadOnlyList<FileResult>> PickMultiplePhotosSafeAsync()
    {
        try
        {
            _multiPickTcs = new TaskCompletionSource<IReadOnlyList<Android.Net.Uri>>();

            Android.Content.Intent intent;
            if (Android.OS.Build.VERSION.SdkInt >= Android.OS.BuildVersionCodes.Tiramisu)
            {
                // API 33+: use the dedicated Android Photo Picker (same UI as MediaPicker)
                intent = new Android.Content.Intent("android.provider.action.PICK_IMAGES");
                intent.PutExtra("android.provider.extra.PICK_IMAGES_MAX", 50);
            }
            else
            {
                // Pre-API 33: ACTION_GET_CONTENT with multi-select opens Google Photos / gallery
                intent = new Android.Content.Intent(Android.Content.Intent.ActionGetContent);
                intent.SetType("image/*");
                intent.AddCategory(Android.Content.Intent.CategoryOpenable);
                intent.PutExtra(Android.Content.Intent.ExtraAllowMultiple, true);
            }

            Platform.CurrentActivity!.StartActivityForResult(intent, MultiPickRequestCode);
            var uris = await _multiPickTcs.Task;

            // Content URIs can't be opened via File.OpenRead — copy each to a temp file
            // so the rest of the compression pipeline works with a regular file path.
            var results = new List<FileResult>();
            var resolver = Platform.CurrentActivity!.ContentResolver!;
            foreach (var uri in uris)
            {
                var tempPath = Path.Combine(FileSystem.CacheDirectory, $"pick_{Guid.NewGuid():N}.jpg");
                using var input = resolver.OpenInputStream(uri)!;
                using var output = File.Create(tempPath);
                await input.CopyToAsync(output);
                results.Add(new FileResult(tempPath));
            }
            return results;
        }
        catch (PermissionException)
        {
            _multiPickTcs = null;
            await Shell.Current.DisplayAlert("Photos", "Photo access was denied. Enable it in Settings > Apps > WorkHub > Permissions.", "OK");
            return [];
        }
    }
#else
    private static async Task<IReadOnlyList<FileResult>> PickMultiplePhotosSafeAsync()
    {
        try
        {
            var options = new PickOptions
            {
                PickerTitle = "Select Photos",
                FileTypes = new FilePickerFileType(new Dictionary<DevicePlatform, IEnumerable<string>>
                {
                    { DevicePlatform.WinUI, [".jpg", ".jpeg", ".png", ".webp"] },
                })
            };
            var results = await FilePicker.PickMultipleAsync(options);
            return results?.ToList() ?? [];
        }
        catch (PermissionException)
        {
            await Shell.Current.DisplayAlert("Photos", "Photo access was denied. Enable it in Settings > Apps > WorkHub > Permissions.", "OK");
            return [];
        }
    }
#endif

    private async Task<List<Models.PhotoResponse>> UploadMultipleAsync(
        IReadOnlyList<FileResult> photos,
        Func<Stream, string, Task<Models.PhotoResponse?>> uploadFunc,
        Action<int, int>? onProgress)
    {
        var results = new List<Models.PhotoResponse>();
        for (int i = 0; i < photos.Count; i++)
        {
            onProgress?.Invoke(i + 1, photos.Count);
            var result = await CompressAndUploadAsync(photos[i], uploadFunc);
            if (result != null) results.Add(result);
        }
        return results;
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