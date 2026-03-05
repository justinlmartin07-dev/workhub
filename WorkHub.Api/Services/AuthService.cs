using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Security.Cryptography;
using System.Text;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;
using WorkHub.Api.Data;
using WorkHub.Api.Models;

namespace WorkHub.Api.Services;

public class AuthService
{
    private readonly WorkHubDbContext _db;
    private readonly IConfiguration _config;

    public AuthService(WorkHubDbContext db, IConfiguration config)
    {
        _db = db;
        _config = config;
    }

    public string GenerateAccessToken(User user)
    {
        var key = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(_config["Jwt:SecretKey"]!));
        var creds = new SigningCredentials(key, SecurityAlgorithms.HmacSha256);

        var claims = new[]
        {
            new Claim(ClaimTypes.NameIdentifier, user.Id.ToString()),
            new Claim(ClaimTypes.Email, user.Email),
            new Claim(ClaimTypes.Name, user.Name),
        };

        var token = new JwtSecurityToken(
            issuer: "workhub-api",
            audience: "workhub-app",
            claims: claims,
            expires: DateTime.UtcNow.AddMinutes(30),
            signingCredentials: creds
        );

        return new JwtSecurityTokenHandler().WriteToken(token);
    }

    public async Task<(string token, RefreshToken entity)> GenerateRefreshToken(Guid userId)
    {
        var rawToken = Convert.ToBase64String(RandomNumberGenerator.GetBytes(64));
        var tokenHash = HashToken(rawToken);

        var refreshToken = new RefreshToken
        {
            Id = Guid.NewGuid(),
            UserId = userId,
            TokenHash = tokenHash,
            ExpiresAt = DateTime.UtcNow.AddDays(30),
            CreatedAt = DateTime.UtcNow,
        };

        _db.RefreshTokens.Add(refreshToken);
        await _db.SaveChangesAsync();

        return (rawToken, refreshToken);
    }

    public async Task<RefreshToken?> ValidateRefreshToken(string rawToken)
    {
        var hash = HashToken(rawToken);
        return await _db.RefreshTokens
            .Include(rt => rt.User)
            .FirstOrDefaultAsync(rt => rt.TokenHash == hash && rt.ExpiresAt > DateTime.UtcNow);
    }

    public async Task RevokeRefreshToken(string rawToken)
    {
        var hash = HashToken(rawToken);
        var token = await _db.RefreshTokens.FirstOrDefaultAsync(rt => rt.TokenHash == hash);
        if (token != null)
        {
            _db.RefreshTokens.Remove(token);
            await _db.SaveChangesAsync();
        }
    }

    public async Task RevokeAllUserTokens(Guid userId)
    {
        var tokens = await _db.RefreshTokens.Where(rt => rt.UserId == userId).ToListAsync();
        _db.RefreshTokens.RemoveRange(tokens);
        await _db.SaveChangesAsync();
    }

    public static string HashToken(string token)
    {
        var bytes = SHA256.HashData(Encoding.UTF8.GetBytes(token));
        return Convert.ToBase64String(bytes);
    }

    public DateTime GetAccessTokenExpiry() => DateTime.UtcNow.AddMinutes(30);
}
