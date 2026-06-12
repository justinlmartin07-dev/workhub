using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
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

    // A valid BCrypt hash to verify against when there's no user (or the account
    // is locked), so the unknown-account path costs the same time as a real one.
    private static readonly string DummyPasswordHash =
        BCrypt.Net.BCrypt.HashPassword("workhub-timing-equalizer");

    private const int LockoutThreshold = 5;

    [HttpPost("login")]
    [AllowAnonymous]
    [EnableRateLimiting("auth")]
    public async Task<IActionResult> Login([FromBody] LoginRequest request)
    {
        var email = request.Email.Trim().ToLowerInvariant();
        var user = await _db.Users.FirstOrDefaultAsync(u => u.Email.ToLower() == email);

        // Unknown email, wrong password, and locked accounts all return the same
        // generic 401, and we always run a BCrypt verify (against a dummy hash when
        // there's no user or the account is locked) so response timing doesn't
        // reveal which emails exist or are currently locked.
        var locked = user is { LockedUntil: { } until } && until > DateTime.UtcNow;
        var hashToCheck = (user != null && !locked) ? user.PasswordHash : DummyPasswordHash;
        var passwordOk = BCrypt.Net.BCrypt.Verify(request.Password, hashToCheck);

        if (user == null || locked || !passwordOk)
        {
            if (user != null && !locked)
            {
                // Real account, wrong password: count the failure and escalate the
                // lockout. The counter is NOT reset when locking, so repeated
                // lockouts lengthen the window instead of granting a fresh 5 tries.
                user.FailedLoginAttempts++;
                if (user.FailedLoginAttempts >= LockoutThreshold)
                {
                    var over = user.FailedLoginAttempts - LockoutThreshold;
                    var minutes = Math.Min(15 * Math.Pow(2, over), 24 * 60);
                    user.LockedUntil = DateTime.UtcNow.AddMinutes(minutes);
                }
                await _db.SaveChangesAsync();
            }
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
    [EnableRateLimiting("auth")]
    public async Task<IActionResult> Refresh([FromBody] RefreshRequest request)
    {
        // Atomic rotation: the old token is consumed (deleted) as part of the
        // lookup, so two concurrent refreshes with the same token can't both win.
        var existing = await _auth.ConsumeRefreshToken(request.RefreshToken);
        if (existing == null)
            return Unauthorized(new ErrorResponse { Error = "Invalid or expired refresh token" });

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
        await _auth.RevokeRefreshToken(request.RefreshToken, this.GetUserId());
        return NoContent();
    }
}
