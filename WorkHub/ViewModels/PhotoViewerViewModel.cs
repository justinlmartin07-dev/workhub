using System.Collections.ObjectModel;
using CommunityToolkit.Maui.Storage;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Microsoft.Maui.ApplicationModel.DataTransfer;
using WorkHub.Models;
using WorkHub.Services;
#if ANDROID
using Android.Content;
using Android.OS;
using Android.Provider;
#endif

namespace WorkHub.ViewModels;

public partial class PhotoViewerViewModel : BaseViewModel
{
    private readonly ApiService _apiService;
    private readonly IHttpClientFactory _httpClientFactory;
    private readonly PhotoCacheService _photoCache;

    [ObservableProperty]
    private ObservableCollection<PhotoDisplayModel> _photos = new();

    [ObservableProperty]
    private int _currentIndex;

    [ObservableProperty]
    private string _title = "Pictures";

    [ObservableProperty]
    private bool _isMenuOpen;

    public event Action? CloseRequested;

    public PhotoViewerViewModel(ApiService apiService, IHttpClientFactory httpClientFactory,
        PhotoCacheService photoCache)
    {
        _apiService = apiService;
        _httpClientFactory = httpClientFactory;
        _photoCache = photoCache;
    }

    // The caller hands over its own (already-resolved) photo collection — the
    // viewer opens with zero network calls, and deletes here update the caller.
    public void Initialize(string title, ObservableCollection<PhotoDisplayModel> photos, int startIndex)
    {
        Title = title;
        Photos = photos;

        if (startIndex >= 0 && startIndex < Photos.Count)
            CurrentIndex = startIndex;

        if (Photos.Count == 0) SetEmpty();
        else SetContent();
        RefreshNavCommands();
    }

    private PhotoResponse? CurrentPhoto => CurrentPhotoModel?.Photo;

    // Bound directly by the Windows photo view (a plain Image) — CarouselView's
    // programmatic Position changes don't scroll backwards on WinUI.
    public PhotoDisplayModel? CurrentPhotoModel =>
        CurrentIndex >= 0 && CurrentIndex < Photos.Count ? Photos[CurrentIndex] : null;

    // Previous/next buttons (Windows — CarouselView swiping is unreliable there).
    [RelayCommand(CanExecute = nameof(CanGoToPrevious))]
    private void PreviousPhoto() => CurrentIndex--;

    private bool CanGoToPrevious() => CurrentIndex > 0;

    [RelayCommand(CanExecute = nameof(CanGoToNext))]
    private void NextPhoto() => CurrentIndex++;

    private bool CanGoToNext() => CurrentIndex < Photos.Count - 1;

    partial void OnCurrentIndexChanged(int value) => RefreshNavCommands();

    private void RefreshNavCommands()
    {
        PreviousPhotoCommand.NotifyCanExecuteChanged();
        NextPhotoCommand.NotifyCanExecuteChanged();
        OnPropertyChanged(nameof(CurrentPhotoModel));
    }

    [RelayCommand]
    private void Close() => CloseRequested?.Invoke();

    [RelayCommand]
    private void ToggleMenu() => IsMenuOpen = !IsMenuOpen;

    [RelayCommand]
    private void CloseMenu() => IsMenuOpen = false;

    [RelayCommand]
    private async Task DeleteCurrentPhotoAsync()
    {
        IsMenuOpen = false;
        var photo = CurrentPhoto;
        if (photo == null) return;
        bool confirm = await Shell.Current.DisplayAlert("Delete Photo", "Delete this photo?", "Delete", "Cancel");
        if (!confirm) return;

        try
        {
            await _apiService.DeletePhotoAsync(photo.Id);
            Photos.RemoveAt(CurrentIndex);
            if (CurrentIndex >= Photos.Count && Photos.Count > 0)
                CurrentIndex = Photos.Count - 1;
            RefreshNavCommands();
            if (Photos.Count == 0)
                CloseRequested?.Invoke();
        }
        catch (Exception ex)
        {
            await Shell.Current.DisplayAlert("Error", ex.Message, "OK");
        }
    }

    [RelayCommand]
    private async Task SaveCurrentPhotoAsync()
    {
        IsMenuOpen = false;
        var photo = CurrentPhoto;
        if (photo == null) return;

        try
        {
            var (bytes, fileName) = await DownloadAsync(photo);
#if WINDOWS
            using var stream = new MemoryStream(bytes);
            var result = await FileSaver.Default.SaveAsync(fileName, stream, CancellationToken.None);
            if (!result.IsSuccessful)
            {
                var msg = result.Exception?.Message ?? "";
                var cancelled = result.Exception is OperationCanceledException
                    || msg.Contains("cancel", StringComparison.OrdinalIgnoreCase);
                if (!cancelled)
                    await Shell.Current.DisplayAlert("Save Failed", msg.Length > 0 ? msg : "Unknown error", "OK");
            }
#elif ANDROID
            await SaveToAndroidGalleryAsync(bytes, fileName);
            await Shell.Current.DisplayAlert("Saved", "Photo saved to your gallery.", "OK");
#endif
        }
        catch (Exception ex)
        {
            await Shell.Current.DisplayAlert("Error", ex.Message, "OK");
        }
    }

    [RelayCommand]
    private async Task ShareCurrentPhotoAsync()
    {
        IsMenuOpen = false;
        var photo = CurrentPhoto;
        if (photo == null) return;

        try
        {
            var (bytes, fileName) = await DownloadAsync(photo);
            var tempPath = Path.Combine(FileSystem.CacheDirectory, fileName);
            await File.WriteAllBytesAsync(tempPath, bytes);

            await Share.Default.RequestAsync(new ShareFileRequest
            {
                Title = "Photo",
                File = new ShareFile(tempPath)
            });
        }
        catch (Exception ex)
        {
            await Shell.Current.DisplayAlert("Error", ex.Message, "OK");
        }
    }

    private async Task<(byte[] Bytes, string FileName)> DownloadAsync(PhotoResponse photo)
    {
        // Prefer the local photo cache — works offline and skips the download.
        var cachedPath = _photoCache.TryGetCachedPath(photo.Id);
        var bytes = cachedPath != null
            ? await File.ReadAllBytesAsync(cachedPath)
            : await _httpClientFactory.CreateClient().GetByteArrayAsync(photo.Url);
        var fileName = SuggestedFileName(photo);
        return (bytes, fileName);
    }

    private static string SuggestedFileName(PhotoResponse photo)
    {
        var ext = ".jpg";
        try
        {
            var uri = new Uri(photo.Url);
            var pathExt = Path.GetExtension(uri.AbsolutePath);
            if (!string.IsNullOrWhiteSpace(pathExt)) ext = pathExt;
        }
        catch { /* fall back to .jpg */ }
        return $"photo_{DateTime.Now:yyyyMMdd_HHmmss}{ext}";
    }

#if ANDROID
    private static async Task SaveToAndroidGalleryAsync(byte[] bytes, string fileName)
    {
        var context = Android.App.Application.Context;
        var resolver = context.ContentResolver
            ?? throw new InvalidOperationException("ContentResolver unavailable");

        var values = new ContentValues();
        values.Put(MediaStore.IMediaColumns.DisplayName, fileName);
        values.Put(MediaStore.IMediaColumns.MimeType, "image/jpeg");

        Android.Net.Uri? collection;
        if (OperatingSystem.IsAndroidVersionAtLeast(29))
        {
            values.Put(MediaStore.IMediaColumns.RelativePath, $"{Android.OS.Environment.DirectoryPictures}/WorkHub");
            values.Put(MediaStore.IMediaColumns.IsPending, 1);
            collection = MediaStore.Images.Media.GetContentUri(MediaStore.VolumeExternalPrimary);
        }
        else
        {
            collection = MediaStore.Images.Media.ExternalContentUri;
        }

        if (collection == null)
            throw new InvalidOperationException("Could not get MediaStore URI");

        var uri = resolver.Insert(collection, values)
            ?? throw new InvalidOperationException("Could not create MediaStore record");

        using (var stream = resolver.OpenOutputStream(uri)
            ?? throw new InvalidOperationException("Could not open output stream"))
        {
            await stream.WriteAsync(bytes, 0, bytes.Length);
        }

        if (OperatingSystem.IsAndroidVersionAtLeast(29))
        {
            values.Clear();
            values.Put(MediaStore.IMediaColumns.IsPending, 0);
            resolver.Update(uri, values, null, null);
        }
    }
#endif
}
