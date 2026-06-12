using System.Text.Json;
using Microsoft.Extensions.Caching.Memory;

namespace WorkHub.Api.Services;

public class AddressService
{
    private readonly HttpClient _httpClient;
    private readonly IMemoryCache _cache;
    private readonly string? _apiKey;

    private static readonly TimeSpan AutocompleteCacheTtl = TimeSpan.FromSeconds(60);
    private static readonly TimeSpan DetailsCacheTtl = TimeSpan.FromHours(24);

    public AddressService(HttpClient httpClient, IMemoryCache cache, IConfiguration configuration)
    {
        _httpClient = httpClient;
        _cache = cache;
        _apiKey = configuration["GOOGLE_PLACES_API_KEY"]
            ?? configuration["Google:PlacesApiKey"];
    }

    public bool IsConfigured => !string.IsNullOrEmpty(_apiKey);

    public async Task<List<AddressSuggestion>> AutocompleteAsync(
        string input,
        (double Lat, double Lng, double RadiusMeters)? bias = null,
        string? sessionToken = null)
    {
        if (!IsConfigured || string.IsNullOrWhiteSpace(input))
            return [];

        var biasKey = bias.HasValue
            ? $"{bias.Value.Lat:F2}:{bias.Value.Lng:F2}:{bias.Value.RadiusMeters:F0}"
            : "nobias";
        var cacheKey = $"places:ac:{input.ToLowerInvariant()}:{biasKey}";
        if (_cache.TryGetValue<List<AddressSuggestion>>(cacheKey, out var cached) && cached is not null)
            return cached;

        var requestBody = new Dictionary<string, object>
        {
            ["input"] = input,
            ["includedRegionCodes"] = new[] { "us" },
            ["languageCode"] = "en"
        };

        if (bias.HasValue)
        {
            requestBody["locationBias"] = new
            {
                circle = new
                {
                    center = new { latitude = bias.Value.Lat, longitude = bias.Value.Lng },
                    radius = bias.Value.RadiusMeters
                }
            };
        }

        if (!string.IsNullOrEmpty(sessionToken))
            requestBody["sessionToken"] = sessionToken;

        var request = new HttpRequestMessage(HttpMethod.Post, "https://places.googleapis.com/v1/places:autocomplete")
        {
            Content = JsonContent.Create(requestBody)
        };
        request.Headers.Add("X-Goog-Api-Key", _apiKey);

        var response = await _httpClient.SendAsync(request);
        if (!response.IsSuccessStatusCode) return [];

        var json = await response.Content.ReadFromJsonAsync<JsonElement>();
        var suggestions = new List<AddressSuggestion>();

        if (json.TryGetProperty("suggestions", out var suggestionsArray))
        {
            foreach (var suggestion in suggestionsArray.EnumerateArray())
            {
                if (!suggestion.TryGetProperty("placePrediction", out var prediction))
                    continue;

                var placeId = prediction.GetProperty("placeId").GetString() ?? "";
                var text = prediction.GetProperty("text").GetProperty("text").GetString() ?? "";
                var mainText = "";
                var secondaryText = "";

                if (prediction.TryGetProperty("structuredFormat", out var sf))
                {
                    mainText = sf.GetProperty("mainText").GetProperty("text").GetString() ?? "";
                    secondaryText = sf.GetProperty("secondaryText").GetProperty("text").GetString() ?? "";
                }

                suggestions.Add(new AddressSuggestion
                {
                    PlaceId = placeId,
                    Description = text,
                    MainText = mainText,
                    SecondaryText = secondaryText
                });
            }
        }

        _cache.Set(cacheKey, suggestions, AutocompleteCacheTtl);
        return suggestions;
    }

    public async Task<AddressDetails?> GetPlaceDetailsAsync(string placeId, string? sessionToken = null)
    {
        if (!IsConfigured || string.IsNullOrWhiteSpace(placeId))
            return null;

        var cacheKey = $"places:details:{placeId}";
        if (_cache.TryGetValue<AddressDetails>(cacheKey, out var cached) && cached is not null)
            return cached;

        var url = $"https://places.googleapis.com/v1/places/{Uri.EscapeDataString(placeId)}";
        if (!string.IsNullOrEmpty(sessionToken))
            url += $"?sessionToken={Uri.EscapeDataString(sessionToken)}";

        var request = new HttpRequestMessage(HttpMethod.Get, url);
        request.Headers.Add("X-Goog-Api-Key", _apiKey);
        request.Headers.Add("X-Goog-FieldMask", "addressComponents,formattedAddress");

        var response = await _httpClient.SendAsync(request);
        if (!response.IsSuccessStatusCode) return null;

        var json = await response.Content.ReadFromJsonAsync<JsonElement>();

        var details = new AddressDetails
        {
            FormattedAddress = json.TryGetProperty("formattedAddress", out var fa) ? fa.GetString() ?? "" : ""
        };

        if (json.TryGetProperty("addressComponents", out var components))
        {
            foreach (var component in components.EnumerateArray())
            {
                var types = component.GetProperty("types").EnumerateArray()
                    .Select(t => t.GetString()).ToList();
                var longText = component.TryGetProperty("longText", out var lt) ? lt.GetString() ?? "" : "";
                var shortText = component.TryGetProperty("shortText", out var st) ? st.GetString() ?? "" : "";

                if (types.Contains("street_number"))
                    details.StreetNumber = longText;
                else if (types.Contains("route"))
                    details.Route = longText;
                else if (types.Contains("locality"))
                    details.City = longText;
                else if (types.Contains("administrative_area_level_1"))
                    details.State = shortText;
                else if (types.Contains("postal_code"))
                    details.Zip = longText;
            }
        }

        details.Street = $"{details.StreetNumber} {details.Route}".Trim();
        _cache.Set(cacheKey, details, DetailsCacheTtl);
        return details;
    }
}

public class AddressSuggestion
{
    public string PlaceId { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public string MainText { get; set; } = string.Empty;
    public string SecondaryText { get; set; } = string.Empty;
}

public class AddressDetails
{
    public string FormattedAddress { get; set; } = string.Empty;
    public string Street { get; set; } = string.Empty;
    public string StreetNumber { get; set; } = string.Empty;
    public string Route { get; set; } = string.Empty;
    public string City { get; set; } = string.Empty;
    public string State { get; set; } = string.Empty;
    public string Zip { get; set; } = string.Empty;
}
