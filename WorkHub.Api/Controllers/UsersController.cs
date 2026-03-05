using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using WorkHub.Api.Data;
using WorkHub.Api.DTOs.Requests;
using WorkHub.Api.DTOs.Responses;
using WorkHub.Api.Services;

namespace WorkHub.Api.Controllers;

[ApiController]
[Route("v1")]
[Authorize]
public class UsersController : ControllerBase
{
    private readonly WorkHubDbContext _db;
    private readonly PhotoService _photos;
    private readonly AuthService _auth;

    public UsersController(WorkHubDbContext db, PhotoService photos, AuthService auth)
    {
        _db = db;
        _photos = photos;
        _auth = auth;
    }

    [HttpGet("users")]
    public async Task<IActionResult> List()
    {
        var users = await _db.Users
            .OrderBy(u => u.Name)
            .Select(u => new UserBriefResponse
            {
                Id = u.Id,
                Name = u.Name,
                Email = u.Email,
                ProfilePhotoUrl = u.ProfilePhotoR2Key != null ? _photos.GeneratePresignedUrl(u.ProfilePhotoR2Key) : null,
            })
            .ToListAsync();

        return Ok(users);
    }

    [HttpGet("me")]
    public async Task<IActionResult> GetProfile()
    {
        var userId = this.GetUserId();
        var user = await _db.Users.FindAsync(userId);
        if (user == null)
            return NotFound(new ErrorResponse { Error = "User not found" });

        return Ok(new UserProfileResponse
        {
            Id = user.Id,
            Name = user.Name,
            Email = user.Email,
            ProfilePhotoUrl = user.ProfilePhotoR2Key != null ? _photos.GeneratePresignedUrl(user.ProfilePhotoR2Key) : null,
            CreatedAt = user.CreatedAt,
        });
    }

    [HttpPut("me")]
    public async Task<IActionResult> UpdateProfile([FromBody] UpdateProfileRequest request)
    {
        var userId = this.GetUserId();
        var user = await _db.Users.FindAsync(userId);
        if (user == null)
            return NotFound(new ErrorResponse { Error = "User not found" });

        user.Name = request.Name;
        await _db.SaveChangesAsync();

        return Ok(new UserProfileResponse
        {
            Id = user.Id,
            Name = user.Name,
            Email = user.Email,
            ProfilePhotoUrl = user.ProfilePhotoR2Key != null ? _photos.GeneratePresignedUrl(user.ProfilePhotoR2Key) : null,
            CreatedAt = user.CreatedAt,
        });
    }

    [HttpPut("me/password")]
    public async Task<IActionResult> ChangePassword([FromBody] ChangePasswordRequest request)
    {
        var userId = this.GetUserId();
        var user = await _db.Users.FindAsync(userId);
        if (user == null)
            return NotFound(new ErrorResponse { Error = "User not found" });

        if (!BCrypt.Net.BCrypt.Verify(request.CurrentPassword, user.PasswordHash))
            return BadRequest(new ErrorResponse { Error = "Current password is incorrect" });

        user.PasswordHash = BCrypt.Net.BCrypt.HashPassword(request.NewPassword);
        await _db.SaveChangesAsync();

        // Revoke all sessions
        await _auth.RevokeAllUserTokens(userId);

        return NoContent();
    }

    [HttpPost("me/photo")]
    public async Task<IActionResult> UploadProfilePhoto(IFormFile file)
    {
        var userId = this.GetUserId();
        var user = await _db.Users.FindAsync(userId);
        if (user == null)
            return NotFound(new ErrorResponse { Error = "User not found" });

        // Delete old photo if exists
        if (user.ProfilePhotoR2Key != null)
            await _photos.DeleteAsync(user.ProfilePhotoR2Key);

        var photoId = Guid.NewGuid();
        var objectKey = $"profiles/{userId}/{photoId}.jpg";

        using var stream = file.OpenReadStream();
        await _photos.UploadAsync(objectKey, stream, file.ContentType);

        user.ProfilePhotoR2Key = objectKey;
        await _db.SaveChangesAsync();

        return Ok(new PhotoResponse
        {
            Id = photoId,
            Url = _photos.GeneratePresignedUrl(objectKey),
            FileName = file.FileName,
            UploadedAt = DateTime.UtcNow,
        });
    }

    [HttpDelete("me/photo")]
    public async Task<IActionResult> DeleteProfilePhoto()
    {
        var userId = this.GetUserId();
        var user = await _db.Users.FindAsync(userId);
        if (user == null)
            return NotFound(new ErrorResponse { Error = "User not found" });

        if (user.ProfilePhotoR2Key != null)
        {
            await _photos.DeleteAsync(user.ProfilePhotoR2Key);
            user.ProfilePhotoR2Key = null;
            await _db.SaveChangesAsync();
        }

        return NoContent();
    }
}
