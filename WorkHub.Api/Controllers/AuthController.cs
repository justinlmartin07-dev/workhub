using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using WorkHub.Api.Data;
using WorkHub.Api.DTOs.Requests;
using WorkHub.Api.DTOs.Responses;
using WorkHub.Api.Services;

namespace WorkHub.Api.Controllers;

[ApiController]
[Route("v1/auth")]
public class AuthController : ControllerBase
{
    private readonly WorkHubDbContext _db;
    private readonly AuthService _auth;
    private readonly PhotoService _photos;

    public AuthController(WorkHubDbContext db, AuthService auth, PhotoService photos)
    {
        _db = db;
        _auth = auth;
        _photos = photos;
    }

    [HttpPost("login")]
    [AllowAnonymous]
    public async Task<IActionResult> Login([FromBody] LoginRequest request)
    {
        var user = await _db.Users.FirstOrDefaultAsync(u => u.Email == request.Email);
        if (user == null)
            return Unauthorized(new ErrorResponse { Error = "Invalid email or password" });

        if (user.LockedUntil.HasValue && user.LockedUntil > DateTime.UtcNow)
        {
            var minutes = (int)Math.Ceiling((user.LockedUntil.Value - DateTime.UtcNow).TotalMinutes);
            return StatusCode(423, new ErrorResponse { Error = $"Account locked. Try again in {minutes} minutes." });
        }

        if (!BCrypt.Net.BCrypt.Verify(request.Password, user.PasswordHash))
        {
            user.FailedLoginAttempts++;
            if (user.FailedLoginAttempts >= 5)
            {
                user.LockedUntil = DateTime.UtcNow.AddMinutes(15);
                user.FailedLoginAttempts = 0;
            }
            await _db.SaveChangesAsync();
            return Unauthorized(new ErrorResponse { Error = "Invalid email or password" });
        }

        user.FailedLoginAttempts = 0;
        user.LockedUntil = null;
        await _db.SaveChangesAsync();

        var accessToken = _auth.GenerateAccessToken(user);
        var (refreshToken, _) = await _auth.GenerateRefreshToken(user.Id);

        return Ok(new LoginResponse
        {
            AccessToken = accessToken,
            RefreshToken = refreshToken,
            ExpiresAt = _auth.GetAccessTokenExpiry(),
            User = new UserBriefResponse
            {
                Id = user.Id,
                Name = user.Name,
                Email = user.Email,
                ProfilePhotoUrl = user.ProfilePhotoR2Key != null ? _photos.GeneratePresignedUrl(user.ProfilePhotoR2Key) : null,
            }
        });
    }

    [HttpPost("refresh")]
    [AllowAnonymous]
    public async Task<IActionResult> Refresh([FromBody] RefreshRequest request)
    {
        var existing = await _auth.ValidateRefreshToken(request.RefreshToken);
        if (existing == null)
            return Unauthorized(new ErrorResponse { Error = "Invalid or expired refresh token" });

        // Rotation: delete old, create new
        _db.RefreshTokens.Remove(existing);
        await _db.SaveChangesAsync();

        var accessToken = _auth.GenerateAccessToken(existing.User);
        var (newRefreshToken, _) = await _auth.GenerateRefreshToken(existing.UserId);

        return Ok(new RefreshResponse
        {
            AccessToken = accessToken,
            RefreshToken = newRefreshToken,
            ExpiresAt = _auth.GetAccessTokenExpiry(),
        });
    }

    [HttpPost("logout")]
    [Authorize]
    public async Task<IActionResult> Logout([FromBody] LogoutRequest request)
    {
        await _auth.RevokeRefreshToken(request.RefreshToken);
        return NoContent();
    }
}
