using WorkHub.Models;

namespace WorkHub.Services;

// Resolves print templates: fresh from the API when reachable (so template
// edits pushed with an API deploy take effect on the very next print), else
// the last-fetched copy from cache, else the copies embedded at build time.
public class PrintTemplateService
{
    private const string CacheKey = "print-templates";
    private static readonly TimeSpan FetchTimeout = TimeSpan.FromSeconds(5);

    private readonly ApiService _apiService;
    private readonly ListCacheService _listCache;

    public PrintTemplateService(ApiService apiService, ListCacheService listCache)
    {
        _apiService = apiService;
        _listCache = listCache;
    }

    public Task<string> GetJobTemplateAsync()
        => GetTemplateAsync(t => t.JobSummary, "job-summary.html");

    public Task<string> GetCustomerTemplateAsync()
        => GetTemplateAsync(t => t.CustomerSummary, "customer-summary.html");

    private async Task<string> GetTemplateAsync(Func<PrintTemplatesResponse, string?> pick, string assetName)
    {
        try
        {
            using var cts = new CancellationTokenSource(FetchTimeout);
            var fresh = await _apiService.GetPrintTemplatesAsync(cts.Token);
            if (fresh != null && !string.IsNullOrWhiteSpace(pick(fresh)))
            {
                _ = _listCache.SaveObjectAsync(CacheKey, fresh);
                return pick(fresh)!;
            }
        }
        catch
        {
            // Offline, timed out, or an older API without the endpoint — fall through.
        }

        var cached = await _listCache.LoadObjectAsync<PrintTemplatesResponse>(CacheKey);
        if (cached != null && !string.IsNullOrWhiteSpace(pick(cached)))
            return pick(cached)!;

        using var stream = await FileSystem.OpenAppPackageFileAsync($"PrintTemplates/{assetName}");
        using var reader = new StreamReader(stream);
        return await reader.ReadToEndAsync();
    }
}
