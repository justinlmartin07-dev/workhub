namespace WorkHub.Api.DTOs.Responses;

public class LoginResponse
{
    public string AccessToken { get; set; } = string.Empty;
    public string RefreshToken { get; set; } = string.Empty;
    public DateTime ExpiresAt { get; set; }
    public UserBriefResponse User { get; set; } = null!;
}

public class RefreshResponse
{
    public string AccessToken { get; set; } = string.Empty;
    public string RefreshToken { get; set; } = string.Empty;
    public DateTime ExpiresAt { get; set; }
}

public class VersionResponse
{
    public string ApiVersion { get; set; } = "1.0.0";
    public string MinimumAppVersion { get; set; } = "1.0.0";
}
