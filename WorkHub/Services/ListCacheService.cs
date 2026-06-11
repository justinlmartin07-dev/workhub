using System.Collections.Concurrent;
using System.Text.Json;

namespace WorkHub.Services;

// Last-known-good copies of the app's lists, persisted as JSON in app data.
// On launch the ViewModels show this cached data immediately and then merge in
// fresh data from the API in the background — so the app is usable in well under
// a second even on a cold (or unreachable) server.
public class ListCacheService
{
    private static readonly JsonSerializerOptions JsonOptions = new() { PropertyNameCaseInsensitive = true };

    private readonly string _dir = Path.Combine(FileSystem.AppDataDirectory, "list-cache");

    private string PathFor(string key) => Path.Combine(_dir, $"{key}.json");

    public async Task<List<T>?> LoadAsync<T>(string key)
    {
        try
        {
            var path = PathFor(key);
            if (!File.Exists(path)) return null;
            await using var stream = File.OpenRead(path);
            return await JsonSerializer.DeserializeAsync<List<T>>(stream, JsonOptions);
        }
        catch
        {
            return null; // corrupt/unreadable cache is the same as no cache
        }
    }

    // Fire-and-forget friendly: serialization happens off the UI thread and
    // failures are swallowed — the cache is an optimization, never a requirement.
    public async Task SaveAsync<T>(string key, IReadOnlyList<T> items)
    {
        try
        {
            await Task.Run(async () =>
            {
                Directory.CreateDirectory(_dir);
                var tmp = PathFor(key) + ".tmp";
                await using (var stream = File.Create(tmp))
                {
                    await JsonSerializer.SerializeAsync(stream, items, JsonOptions);
                }
                File.Move(tmp, PathFor(key), overwrite: true);
            });
        }
        catch
        {
        }
    }

    // ── Single-object cache (detail responses) ──
    // Backed by the same JSON files plus an in-memory layer, so re-opening a
    // detail within a session never even touches disk. Values are stored as
    // serialized JSON and deserialized fresh on every load — callers always get
    // their own instance, never an alias of a live data-bound object.

    private readonly ConcurrentDictionary<string, string> _memory = new();

    public async Task<T?> LoadObjectAsync<T>(string key) where T : class
    {
        try
        {
            if (_memory.TryGetValue(key, out var json))
                return JsonSerializer.Deserialize<T>(json, JsonOptions);

            var path = PathFor(key);
            if (!File.Exists(path)) return null;
            json = await Task.Run(() => File.ReadAllText(path));
            _memory[key] = json;
            return JsonSerializer.Deserialize<T>(json, JsonOptions);
        }
        catch
        {
            return null;
        }
    }

    public async Task SaveObjectAsync<T>(string key, T value) where T : class
    {
        try
        {
            var json = JsonSerializer.Serialize(value, JsonOptions);
            _memory[key] = json;
            await Task.Run(() =>
            {
                Directory.CreateDirectory(_dir);
                var tmp = PathFor(key) + ".tmp";
                File.WriteAllText(tmp, json);
                File.Move(tmp, PathFor(key), overwrite: true);
            });
        }
        catch
        {
        }
    }

    public void Remove(string key)
    {
        _memory.TryRemove(key, out _);
        try
        {
            var path = PathFor(key);
            if (File.Exists(path)) File.Delete(path);
        }
        catch
        {
        }
    }

    public void Clear()
    {
        _memory.Clear();
        try
        {
            if (Directory.Exists(_dir))
                Directory.Delete(_dir, recursive: true);
        }
        catch
        {
        }
    }
}
