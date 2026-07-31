using System.Net.Http.Json;
using CommunityToolkit.Mvvm.Messaging;
using WorkHub.Messages;
using WorkHub.Models;

namespace WorkHub.Services;

public class AuthService
{
    private readonly HttpClient _httpClient;
    private string? _accessToken;
    private string? _refreshToken;
    private DateTime _expiresAt;
    private UserBriefResponse? _currentUser;

    private const string AccessTokenKey = "access_token";
    private const string RefreshTokenKey = "refresh_token";
    private const string ExpiresAtKey = "expires_at";
    private const string UserIdKey = "user_id";
    private const string UserNameKey = "user_name";
    private const string UserEmailKey = "user_email";
    private const string UserPhotoUrlKey = "user_photo_url";

    public UserBriefResponse? CurrentUser => _currentUser;
    public bool IsAuthenticated => !string.IsNullOrEmpty(_accessToken) && _expiresAt > DateTime.UtcNow;

    private readonly ListCacheService _listCache;
    private readonly PhotoCacheService _photoCache;

    public AuthService(IHttpClientFactory httpClientFactory, ListCacheService listCache, PhotoCacheService photoCache)
    {
        _httpClient = httpClientFactory.CreateClient("AuthClient");
        _listCache = listCache;
        _photoCache = photoCache;
    }

    public async Task<bool> TryRestoreSessionAsync()
    {
        try
        {
            _accessToken = await SecureStorage.GetAsync(AccessTokenKey);
            _refreshToken = await SecureStorage.GetAsync(RefreshTokenKey);
            var expiresStr = await SecureStorage.GetAsync(ExpiresAtKey);
            if (expiresStr != null) _expiresAt = DateTime.Parse(expiresStr);

            var userId = await SecureStorage.GetAsync(UserIdKey);
            var userName = await SecureStorage.GetAsync(UserNameKey);
            var userEmail = await SecureStorage.GetAsync(UserEmailKey);

            var userPhotoUrl = await SecureStorage.GetAsync(UserPhotoUrlKey);

            if (userId != null && userName != null && userEmail != null)
            {
                _currentUser = new UserBriefResponse
                {
                    Id = Guid.Parse(userId),
                    Name = userName,
                    Email = userEmail,
                    ProfilePhotoUrl = string.IsNullOrEmpty(userPhotoUrl) ? null : userPhotoUrl
                };
            }

            return !string.IsNullOrEmpty(_accessToken) && !string.IsNullOrEmpty(_refreshToken);
        }
        catch
        {
            return false;
        }
    }

    public async Task<(bool Success, string? Error)> LoginAsync(string email, string password)
    {
        try
        {
            var request = new LoginRequest { Email = email, Password = password };
            var response = await _httpClient.PostAsJsonAsync("v1/auth/login", request);

            if (!response.IsSuccessStatusCode)
            {
                var error = await response.Content.ReadFromJsonAsync<ErrorResponse>();
                return (false, error?.Error ?? "Login failed");
            }

            var result = await response.Content.ReadFromJsonAsync<LoginResponse>();
            if (result == null) return (false, "Invalid response");

            _accessToken = result.AccessToken;
            _refreshToken = result.RefreshToken;
            _expiresAt = result.ExpiresAt;
            _currentUser = result.User;

            await SaveTokensAsync();
            return (true, null);
        }
        catch (HttpRequestException)
        {
            return (false, "Unable to connect to server");
        }
        catch (Exception ex)
        {
            return (false, ex.Message);
        }
    }

    private readonly SemaphoreSlim _refreshLock = new(1, 1);

    public async Task<string?> GetValidTokenAsync()
    {
        if (_expiresAt > DateTime.UtcNow.AddMinutes(2))
            return _accessToken;

        // Refresh tokens rotate on use — concurrent requests (e.g. parallel list
        // loads at launch) must not race the refresh call, or the loser presents
        // an already-rotated token and gets logged out.
        await _refreshLock.WaitAsync();
        try
        {
            if (_expiresAt > DateTime.UtcNow.AddMinutes(2))
                return _accessToken;

            if (string.IsNullOrEmpty(_refreshToken))
                return null;

            try
            {
                var request = new { RefreshToken = _refreshToken };
                var response = await _httpClient.PostAsJsonAsync("v1/auth/refresh", request);

                if (!response.IsSuccessStatusCode)
                {
                    await LogoutAsync();
                    return null;
                }

                var result = await response.Content.ReadFromJsonAsync<RefreshResponse>();
                if (result == null) return null;

                _accessToken = result.AccessToken;
                _refreshToken = result.RefreshToken;
                _expiresAt = result.ExpiresAt;
                await SaveTokensAsync();
                return _accessToken;
            }
            catch
            {
                return _accessToken;
            }
        }
        finally
        {
            _refreshLock.Release();
        }
    }

    public async Task LogoutAsync()
    {
        if (!string.IsNullOrEmpty(_refreshToken))
        {
            try
            {
                var request = new { RefreshToken = _refreshToken };
                await _httpClient.PostAsJsonAsync("v1/auth/logout", request);
            }
            catch { }
        }

        _accessToken = null;
        _refreshToken = null;
        _currentUser = null;
        SecureStorage.RemoveAll();
        _listCache.Clear();
        _photoCache.Clear();
    }

    public async Task<VersionResponse?> CheckVersionAsync()
    {
        try
        {
            return await _httpClient.GetFromJsonAsync<VersionResponse>("v1/version");
        }
        catch
        {
            return null;
        }
    }

    public void UpdateCurrentUserPhoto(string? photoUrl)
    {
        if (_currentUser != null)
            _currentUser.ProfilePhotoUrl = photoUrl;
        _ = SetOrRemoveAsync(UserPhotoUrlKey, photoUrl);
        WeakReferenceMessenger.Default.Send(new DataChangedMessage("user_photo"));
    }

    private async Task SaveTokensAsync()
    {
        await SetOrRemoveAsync(AccessTokenKey, _accessToken);
        await SetOrRemoveAsync(RefreshTokenKey, _refreshToken);
        await SetOrRemoveAsync(ExpiresAtKey, _expiresAt.ToString("O"));
        if (_currentUser != null)
        {
            await SetOrRemoveAsync(UserIdKey, _currentUser.Id.ToString());
            await SetOrRemoveAsync(UserNameKey, _currentUser.Name);
            await SetOrRemoveAsync(UserEmailKey, _currentUser.Email);
            await SetOrRemoveAsync(UserPhotoUrlKey, _currentUser.ProfilePhotoUrl);
        }
    }

    // Windows SecureStorage protects values via WinRT DataProtectionProvider, which
    // throws E_INVALIDARG ("Value does not fall within the expected range") on an
    // empty string — remove the key instead of storing "".
    private static async Task SetOrRemoveAsync(string key, string? value)
    {
        if (string.IsNullOrEmpty(value))
            SecureStorage.Remove(key);
        else
            await SecureStorage.SetAsync(key, value);
    }
}