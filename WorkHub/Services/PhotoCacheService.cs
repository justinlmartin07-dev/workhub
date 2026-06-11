using System.Collections.Concurrent;

namespace WorkHub.Services;

// Photo files cached on disk keyed by the photo's stable Id. Presigned R2 URLs
// change on every API response, which defeats MAUI's URI-keyed image cache —
// caching by Id means each photo is downloaded exactly once, ever.
public class PhotoCacheService
{
    private readonly string _dir = Path.Combine(FileSystem.CacheDirectory, "photos");
    private readonly IHttpClientFactory _httpClientFactory;
    private readonly ConcurrentDictionary<Guid, Task<string?>> _inFlight = new();

    public PhotoCacheService(IHttpClientFactory httpClientFactory)
    {
        _httpClientFactory = httpClientFactory;
    }

    private string PathFor(Guid photoId) => Path.Combine(_dir, $"{photoId}.jpg");

    public string? TryGetCachedPath(Guid photoId)
    {
        var path = PathFor(photoId);
        return File.Exists(path) ? path : null;
    }

    // Returns the local file path, downloading once if needed. Concurrent calls
    // for the same photo share one download. Returns null on any failure
    // (offline, expired presigned URL) — never throws.
    public Task<string?> GetOrDownloadAsync(Guid photoId, string url)
    {
        var cached = TryGetCachedPath(photoId);
        if (cached != null) return Task.FromResult<string?>(cached);

        return _inFlight.GetOrAdd(photoId, _ => DownloadAsync(photoId, url));
    }

    private async Task<string?> DownloadAsync(Guid photoId, string url)
    {
        try
        {
            // Plain client: presigned URLs carry their own auth, and the API's
            // bearer token must not be sent to R2.
            var http = _httpClientFactory.CreateClient();
            var bytes = await http.GetByteArrayAsync(url);

            Directory.CreateDirectory(_dir);
            var path = PathFor(photoId);
            var tmp = path + ".tmp";
            await File.WriteAllBytesAsync(tmp, bytes);
            File.Move(tmp, path, overwrite: true);
            return path;
        }
        catch
        {
            return null;
        }
        finally
        {
            _inFlight.TryRemove(photoId, out _);
        }
    }

    public void Clear()
    {
        try
        {
            if (Directory.Exists(_dir))
                Directory.Delete(_dir, recursive: true);
        }
        catch
        {
        }
    }

    // Simple size bound: when the cache exceeds the cap, delete oldest files
    // until under 75% of it. Called once at startup.
    public Task TrimAsync(long maxBytes = 200 * 1024 * 1024) => Task.Run(() =>
    {
        try
        {
            if (!Directory.Exists(_dir)) return;
            var files = new DirectoryInfo(_dir).GetFiles();
            var total = files.Sum(f => f.Length);
            if (total <= maxBytes) return;

            foreach (var file in files.OrderBy(f => f.LastWriteTimeUtc))
            {
                try
                {
                    total -= file.Length;
                    file.Delete();
                }
                catch { }
                if (total <= maxBytes * 3 / 4) break;
            }
        }
        catch
        {
        }
    });
}
