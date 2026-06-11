using CommunityToolkit.Mvvm.ComponentModel;
using WorkHub.Models;
using WorkHub.Services;

namespace WorkHub.ViewModels;

// Wraps a PhotoResponse for display so the bound ImageSource is decoupled from
// the presigned URL (which changes on every fetch). Merged by Id across detail
// refreshes — an already-rendered thumbnail keeps its Source untouched, so the
// image never flickers or re-downloads.
public partial class PhotoDisplayModel : ObservableObject
{
    public PhotoResponse Photo { get; private set; }
    public Guid Id => Photo.Id;

    [ObservableProperty]
    private ImageSource? _source;

    public bool IsResolvedLocally { get; private set; }

    public PhotoDisplayModel(PhotoResponse photo)
    {
        Photo = photo;
    }

    // Adopt a fresh response (new presigned URL) without touching Source.
    public void UpdatePhoto(PhotoResponse fresh) => Photo = fresh;

    // Point Source at the local cached file, downloading if needed.
    // urlIsFresh: whether Photo.Url came straight from the API (live) rather
    // than from the disk cache (possibly expired). A dead URL is never bound
    // directly — better a blank tile for a moment than a broken-image flash.
    public async Task ResolveAsync(PhotoCacheService cache, bool urlIsFresh)
    {
        if (IsResolvedLocally) return;

        var path = await cache.GetOrDownloadAsync(Photo.Id, Photo.Url);
        if (path != null)
        {
            IsResolvedLocally = true;
            Source = ImageSource.FromFile(path);
        }
        else if (urlIsFresh)
        {
            Source = Photo.Url;
        }
    }
}
