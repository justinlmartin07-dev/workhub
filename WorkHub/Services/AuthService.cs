using System.Net.Http.Json;
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

    public UserBriefResponse? CurrentUser => _currentUser;
    public bool IsAuthenticated => !string.IsNullOrEmpty(_accessToken) && _expiresAt > DateTime.UtcNow;

    public AuthService(IHttpClientFactory httpClientFactory)
    {
        _httpClient = httpClientFactory.CreateClient("AuthClient");
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

            if (userId != null && userName != null && userEmail != null)
            {
                _currentUser = new UserBriefResponse
                {
                    Id = Guid.Parse(userId),
                    Name = userName,
                    Email = userEmail
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

    public async Task<string?> GetValidTokenAsync()
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

    private async Task SaveTokensAsync()
    {
        await SecureStorage.SetAsync(AccessTokenKey, _accessToken ?? "");
        await SecureStorage.SetAsync(RefreshTokenKey, _refreshToken ?? "");
        await SecureStorage.SetAsync(ExpiresAtKey, _expiresAt.ToString("O"));
        if (_currentUser != null)
        {
            await SecureStorage.SetAsync(UserIdKey, _currentUser.Id.ToString());
            await SecureStorage.SetAsync(UserNameKey, _currentUser.Name);
            await SecureStorage.SetAsync(UserEmailKey, _currentUser.Email);
        }
    }
}