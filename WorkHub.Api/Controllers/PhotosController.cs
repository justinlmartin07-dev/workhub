using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using WorkHub.Api.Data;
using WorkHub.Api.DTOs.Responses;
using WorkHub.Api.Models;
using WorkHub.Api.Services;

namespace WorkHub.Api.Controllers;

[ApiController]
[Route("v1")]
[Authorize]
public class PhotosController : ControllerBase
{
    private readonly WorkHubDbContext _db;
    private readonly PhotoService _photos;
    private readonly ILogger<PhotosController> _logger;

    public PhotosController(WorkHubDbContext db, PhotoService photos, ILogger<PhotosController> logger)
    {
        _db = db;
        _photos = photos;
        _logger = logger;
    }

    [HttpPost("customers/{customerId:guid}/photos")]
    public async Task<IActionResult> UploadCustomerPhoto(Guid customerId, IFormFile file)
    {
        if (file == null || file.Length == 0)
            return BadRequest(new ErrorResponse { Error = "No file provided" });
        if (file.Length > PhotoService.MaxFileBytes)
            return BadRequest(new ErrorResponse { Error = "File too large (max 15 MB)" });
        if (!PhotoService.IsAllowedImage(file.ContentType))
            return BadRequest(new ErrorResponse { Error = "Unsupported file type. Allowed: JPEG, PNG, WebP." });

        var customer = await _db.Customers.FirstOrDefaultAsync(c => c.Id == customerId && c.DeletedAt == null);
        if (customer == null)
            return NotFound(new ErrorResponse { Error = "Customer not found" });

        var photoId = Guid.NewGuid();
        var objectKey = $"customers/{customerId}/{photoId}.jpg";

        try
        {
            using var stream = file.OpenReadStream();
            await _photos.UploadAsync(objectKey, stream, file.ContentType);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to upload customer photo for {CustomerId}", customerId);
            return StatusCode(500, new ErrorResponse { Error = "Photo upload failed" });
        }

        var photo = new CustomerPhoto
        {
            Id = photoId,
            CustomerId = customerId,
            R2ObjectKey = objectKey,
            FileName = file.FileName,
            AddressTag = AddressNormalizer.Normalize(customer.Address),
            UploadedAt = DateTime.UtcNow,
        };

        _db.CustomerPhotos.Add(photo);
        await _db.SaveChangesAsync();

        return Created($"v1/photos/{photo.Id}", new PhotoResponse
        {
            Id = photo.Id,
            Url = _photos.GeneratePresignedUrl(objectKey),
            FileName = photo.FileName,
            UploadedAt = photo.UploadedAt,
        });
    }

    [HttpPost("jobs/{jobId:guid}/photos")]
    public async Task<IActionResult> UploadJobPhoto(Guid jobId, IFormFile file)
    {
        if (file == null || file.Length == 0)
            return BadRequest(new ErrorResponse { Error = "No file provided" });
        if (file.Length > PhotoService.MaxFileBytes)
            return BadRequest(new ErrorResponse { Error = "File too large (max 15 MB)" });
        if (!PhotoService.IsAllowedImage(file.ContentType))
            return BadRequest(new ErrorResponse { Error = "Unsupported file type. Allowed: JPEG, PNG, WebP." });

        var job = await _db.Jobs.Include(j => j.Customer).FirstOrDefaultAsync(j => j.Id == jobId && j.DeletedAt == null);
        if (job == null)
            return NotFound(new ErrorResponse { Error = "Job not found" });

        var photoId = Guid.NewGuid();
        var objectKey = $"jobs/{jobId}/{photoId}.jpg";

        try
        {
            using var stream = file.OpenReadStream();
            await _photos.UploadAsync(objectKey, stream, file.ContentType);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to upload job photo for {JobId}", jobId);
            return StatusCode(500, new ErrorResponse { Error = "Photo upload failed" });
        }

        var photo = new JobPhoto
        {
            Id = photoId,
            JobId = jobId,
            R2ObjectKey = objectKey,
            FileName = file.FileName,
            AddressTag = AddressNormalizer.Normalize(job.Customer.Address),
            UploadedAt = DateTime.UtcNow,
        };

        if (job.Status == "New")
            job.Status = "In Progress";

        _db.JobPhotos.Add(photo);
        await _db.SaveChangesAsync();

        return Created($"v1/photos/{photo.Id}", new PhotoResponse
        {
            Id = photo.Id,
            Url = _photos.GeneratePresignedUrl(objectKey),
            FileName = photo.FileName,
            UploadedAt = photo.UploadedAt,
        });
    }

    [HttpDelete("photos/{id:guid}")]
    public async Task<IActionResult> DeletePhoto(Guid id)
    {
        // Check customer photos first
        var customerPhoto = await _db.CustomerPhotos.FindAsync(id);
        if (customerPhoto != null)
        {
            await _photos.DeleteAsync(customerPhoto.R2ObjectKey);
            _db.CustomerPhotos.Remove(customerPhoto);
            await _db.SaveChangesAsync();
            return NoContent();
        }

        // Check job photos
        var jobPhoto = await _db.JobPhotos.FindAsync(id);
        if (jobPhoto != null)
        {
            await _photos.DeleteAsync(jobPhoto.R2ObjectKey);
            _db.JobPhotos.Remove(jobPhoto);
            await _db.SaveChangesAsync();
            return NoContent();
        }

        return NotFound(new ErrorResponse { Error = "Photo not found" });
    }

    [HttpGet("photos/by-address/count")]
    public async Task<IActionResult> CountByAddress(
        [FromQuery] string address,
        [FromQuery] Guid? excludeCustomerId,
        [FromQuery] Guid? excludeJobId)
    {
        var normalized = AddressNormalizer.Normalize(address);
        if (normalized == null)
            return Ok(0);

        var customerPhotoCount = await _db.CustomerPhotos
            .Where(p => p.AddressTag == normalized)
            .Where(p => !excludeCustomerId.HasValue || p.CustomerId != excludeCustomerId)
            .CountAsync();

        var jobPhotoCount = await _db.JobPhotos
            .Where(p => p.AddressTag == normalized)
            .Where(p => !excludeJobId.HasValue || p.JobId != excludeJobId)
            .CountAsync();

        return Ok(customerPhotoCount + jobPhotoCount);
    }

    [HttpGet("photos/by-address")]
    public async Task<IActionResult> GetByAddress(
        [FromQuery] string address,
        [FromQuery] Guid? excludeCustomerId,
        [FromQuery] Guid? excludeJobId)
    {
        var normalized = AddressNormalizer.Normalize(address);
        if (normalized == null)
            return Ok(Array.Empty<LocationPhotoGroupResponse>());

        var groups = new List<LocationPhotoGroupResponse>();

        // Customer photo groups
        var customerPhotos = await _db.CustomerPhotos
            .Where(p => p.AddressTag == normalized)
            .Where(p => !excludeCustomerId.HasValue || p.CustomerId != excludeCustomerId)
            .Include(p => p.Customer)
            .OrderByDescending(p => p.UploadedAt)
            .ToListAsync();

        foreach (var group in customerPhotos.GroupBy(p => p.CustomerId))
        {
            var first = group.First();
            groups.Add(new LocationPhotoGroupResponse
            {
                CustomerId = first.CustomerId,
                CustomerName = first.Customer.Name,
                Photos = group.Select(p => new PhotoResponse
                {
                    Id = p.Id,
                    Url = _photos.GeneratePresignedUrl(p.R2ObjectKey),
                    FileName = p.FileName,
                    UploadedAt = p.UploadedAt,
                }).ToList(),
            });
        }

        // Job photo groups
        var jobPhotos = await _db.JobPhotos
            .Where(p => p.AddressTag == normalized)
            .Where(p => !excludeJobId.HasValue || p.JobId != excludeJobId)
            .Include(p => p.Job)
            .OrderByDescending(p => p.UploadedAt)
            .ToListAsync();

        foreach (var group in jobPhotos.GroupBy(p => p.JobId))
        {
            var first = group.First();
            groups.Add(new LocationPhotoGroupResponse
            {
                JobId = first.JobId,
                JobTitle = first.Job.Title,
                Photos = group.Select(p => new PhotoResponse
                {
                    Id = p.Id,
                    Url = _photos.GeneratePresignedUrl(p.R2ObjectKey),
                    FileName = p.FileName,
                    UploadedAt = p.UploadedAt,
                }).ToList(),
            });
        }

        // Sort groups by most recent photo
        groups.Sort((a, b) =>
        {
            var aMax = a.Photos.Max(p => p.UploadedAt);
            var bMax = b.Photos.Max(p => p.UploadedAt);
            return bMax.CompareTo(aMax);
        });

        return Ok(groups);
    }
}
