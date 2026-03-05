using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using WorkHub.Api.Data;
using WorkHub.Api.DTOs.Requests;
using WorkHub.Api.DTOs.Responses;
using WorkHub.Api.Models;
using WorkHub.Api.Services;

namespace WorkHub.Api.Controllers;

[ApiController]
[Route("v1/jobs")]
[Authorize]
public class JobsController : ControllerBase
{
    private readonly WorkHubDbContext _db;
    private readonly PhotoService _photos;

    public JobsController(WorkHubDbContext db, PhotoService photos)
    {
        _db = db;
        _photos = photos;
    }

    [HttpGet]
    public async Task<IActionResult> List(
        [FromQuery] string? q,
        [FromQuery] string? status,
        [FromQuery] string? priority,
        [FromQuery] Guid? customerId,
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 25)
    {
        pageSize = Math.Clamp(pageSize, 1, 100);
        page = Math.Max(page, 1);

        var query = _db.Jobs
            .Include(j => j.Customer)
            .Where(j => j.DeletedAt == null);

        if (!string.IsNullOrWhiteSpace(q))
            query = query.Where(j => EF.Functions.ILike(j.Title, $"%{q}%")
                || (j.ScopeNotes != null && EF.Functions.ILike(j.ScopeNotes, $"%{q}%")));

        if (!string.IsNullOrWhiteSpace(status))
            query = query.Where(j => j.Status == status);

        if (!string.IsNullOrWhiteSpace(priority))
            query = query.Where(j => j.Priority == priority);

        if (customerId.HasValue)
            query = query.Where(j => j.CustomerId == customerId.Value);

        var totalCount = await query.CountAsync();
        var items = await query
            .OrderByDescending(j => j.CreatedAt)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .Select(j => new JobListItemResponse
            {
                Id = j.Id,
                CustomerId = j.CustomerId,
                CustomerName = j.Customer.Name,
                Title = j.Title,
                Status = j.Status,
                Priority = j.Priority,
                CreatedAt = j.CreatedAt,
            })
            .ToListAsync();

        return Ok(new PagedResponse<JobListItemResponse>
        {
            Items = items,
            Page = page,
            PageSize = pageSize,
            TotalCount = totalCount,
            TotalPages = (int)Math.Ceiling((double)totalCount / pageSize),
        });
    }

    [HttpGet("{id:guid}")]
    public async Task<IActionResult> Get(Guid id)
    {
        var job = await _db.Jobs
            .Where(j => j.Id == id && j.DeletedAt == null)
            .Include(j => j.Customer)
            .Include(j => j.Photos.OrderByDescending(p => p.UploadedAt))
            .Include(j => j.Notes.OrderBy(n => n.CreatedAt))
                .ThenInclude(n => n.CreatedByUser)
            .Include(j => j.InventoryItems)
                .ThenInclude(ji => ji.InventoryItem)
            .Include(j => j.AdhocItems)
            .FirstOrDefaultAsync();

        if (job == null)
            return NotFound(new ErrorResponse { Error = "Job not found" });

        var usedItems = new List<JobItemResponse>();
        var toOrderItems = new List<JobItemResponse>();

        foreach (var ji in job.InventoryItems)
        {
            var item = new JobItemResponse
            {
                Id = ji.Id,
                Name = ji.InventoryItem.Name,
                Description = ji.InventoryItem.Description,
                PartNumber = ji.InventoryItem.PartNumber,
                Quantity = ji.Quantity,
                ListType = ji.ListType,
                Source = "library",
                InventoryItemId = ji.InventoryItemId,
            };
            if (ji.ListType == "used") usedItems.Add(item);
            else toOrderItems.Add(item);
        }

        foreach (var ai in job.AdhocItems)
        {
            var item = new JobItemResponse
            {
                Id = ai.Id,
                Name = ai.Name,
                Description = ai.Description,
                Quantity = ai.Quantity,
                ListType = ai.ListType,
                Source = "adhoc",
            };
            if (ai.ListType == "used") usedItems.Add(item);
            else toOrderItems.Add(item);
        }

        return Ok(new JobResponse
        {
            Id = job.Id,
            CustomerId = job.CustomerId,
            CustomerName = job.Customer.Name,
            Title = job.Title,
            Status = job.Status,
            Priority = job.Priority,
            ScopeNotes = job.ScopeNotes,
            CreatedAt = job.CreatedAt,
            UpdatedAt = job.UpdatedAt,
            Photos = job.Photos.Select(p => new PhotoResponse
            {
                Id = p.Id,
                Url = _photos.GeneratePresignedUrl(p.R2ObjectKey),
                FileName = p.FileName,
                UploadedAt = p.UploadedAt,
            }).ToList(),
            Notes = job.Notes.Select(n => new JobNoteResponse
            {
                Id = n.Id,
                Content = n.Content,
                CreatedBy = n.CreatedBy,
                CreatedByName = n.CreatedByUser.Name,
                CreatedAt = n.CreatedAt,
                UpdatedAt = n.UpdatedAt,
            }).ToList(),
            UsedItems = usedItems,
            ToOrderItems = toOrderItems,
        });
    }

    [HttpPost]
    public async Task<IActionResult> Create([FromBody] CreateJobRequest request)
    {
        var customer = await _db.Customers.FirstOrDefaultAsync(c => c.Id == request.CustomerId && c.DeletedAt == null);
        if (customer == null)
            return BadRequest(new ErrorResponse { Error = "Customer not found" });

        var job = new Job
        {
            Id = Guid.NewGuid(),
            CustomerId = request.CustomerId,
            Title = request.Title,
            Status = request.Status ?? "Pending",
            Priority = request.Priority ?? "Normal",
            ScopeNotes = request.ScopeNotes,
            CreatedBy = this.GetUserId(),
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow,
        };

        _db.Jobs.Add(job);
        await _db.SaveChangesAsync();

        return CreatedAtAction(nameof(Get), new { id = job.Id }, new JobResponse
        {
            Id = job.Id,
            CustomerId = job.CustomerId,
            CustomerName = customer.Name,
            Title = job.Title,
            Status = job.Status,
            Priority = job.Priority,
            ScopeNotes = job.ScopeNotes,
            CreatedAt = job.CreatedAt,
            UpdatedAt = job.UpdatedAt,
        });
    }

    [HttpPut("{id:guid}")]
    public async Task<IActionResult> Update(Guid id, [FromBody] UpdateJobRequest request)
    {
        var job = await _db.Jobs.Include(j => j.Customer).FirstOrDefaultAsync(j => j.Id == id && j.DeletedAt == null);
        if (job == null)
            return NotFound(new ErrorResponse { Error = "Job not found" });

        if (request.Title != null) job.Title = request.Title;
        if (request.Status != null) job.Status = request.Status;
        if (request.Priority != null) job.Priority = request.Priority;
        if (request.ScopeNotes != null) job.ScopeNotes = request.ScopeNotes;
        job.UpdatedAt = DateTime.UtcNow;

        await _db.SaveChangesAsync();

        return Ok(new JobResponse
        {
            Id = job.Id,
            CustomerId = job.CustomerId,
            CustomerName = job.Customer.Name,
            Title = job.Title,
            Status = job.Status,
            Priority = job.Priority,
            ScopeNotes = job.ScopeNotes,
            CreatedAt = job.CreatedAt,
            UpdatedAt = job.UpdatedAt,
        });
    }

    [HttpDelete("{id:guid}")]
    public async Task<IActionResult> Delete(Guid id)
    {
        var job = await _db.Jobs.FirstOrDefaultAsync(j => j.Id == id && j.DeletedAt == null);
        if (job == null)
            return NotFound(new ErrorResponse { Error = "Job not found" });

        job.DeletedAt = DateTime.UtcNow;
        await _db.SaveChangesAsync();
        return NoContent();
    }
}
