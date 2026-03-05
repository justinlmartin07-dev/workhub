using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using WorkHub.Api.Data;
using WorkHub.Api.DTOs.Requests;
using WorkHub.Api.DTOs.Responses;
using WorkHub.Api.Models;

namespace WorkHub.Api.Controllers;

[ApiController]
[Route("v1/jobs/{jobId:guid}/adhoc-items")]
[Authorize]
public class JobAdhocItemsController : ControllerBase
{
    private readonly WorkHubDbContext _db;

    public JobAdhocItemsController(WorkHubDbContext db) => _db = db;

    [HttpGet]
    public async Task<IActionResult> List(Guid jobId, [FromQuery(Name = "list_type")] string? listType)
    {
        var jobExists = await _db.Jobs.AnyAsync(j => j.Id == jobId && j.DeletedAt == null);
        if (!jobExists)
            return NotFound(new ErrorResponse { Error = "Job not found" });

        var query = _db.JobAdhocItems.Where(ai => ai.JobId == jobId);

        if (!string.IsNullOrWhiteSpace(listType))
            query = query.Where(ai => ai.ListType == listType);

        var items = await query
            .Select(ai => new JobItemResponse
            {
                Id = ai.Id,
                Name = ai.Name,
                Description = ai.Description,
                Quantity = ai.Quantity,
                ListType = ai.ListType,
                Source = "adhoc",
            })
            .ToListAsync();

        return Ok(items);
    }

    [HttpPost]
    public async Task<IActionResult> Create(Guid jobId, [FromBody] CreateJobAdhocItemRequest request)
    {
        var jobExists = await _db.Jobs.AnyAsync(j => j.Id == jobId && j.DeletedAt == null);
        if (!jobExists)
            return NotFound(new ErrorResponse { Error = "Job not found" });

        var item = new JobAdhocItem
        {
            Id = Guid.NewGuid(),
            JobId = jobId,
            Name = request.Name,
            Description = request.Description,
            Quantity = request.Quantity,
            ListType = request.ListType,
            CreatedAt = DateTime.UtcNow,
        };

        _db.JobAdhocItems.Add(item);
        await _db.SaveChangesAsync();

        return Created($"v1/jobs/{jobId}/adhoc-items/{item.Id}", new JobItemResponse
        {
            Id = item.Id,
            Name = item.Name,
            Description = item.Description,
            Quantity = item.Quantity,
            ListType = item.ListType,
            Source = "adhoc",
        });
    }

    [HttpPut("{itemId:guid}")]
    public async Task<IActionResult> Update(Guid jobId, Guid itemId, [FromBody] UpdateJobAdhocItemRequest request)
    {
        var item = await _db.JobAdhocItems.FirstOrDefaultAsync(ai => ai.Id == itemId && ai.JobId == jobId);
        if (item == null)
            return NotFound(new ErrorResponse { Error = "Ad-hoc item not found" });

        if (request.Name != null) item.Name = request.Name;
        if (request.Description != null) item.Description = request.Description;
        if (request.Quantity.HasValue) item.Quantity = request.Quantity.Value;
        if (request.ListType != null) item.ListType = request.ListType;
        item.UpdatedAt = DateTime.UtcNow;

        await _db.SaveChangesAsync();

        return Ok(new JobItemResponse
        {
            Id = item.Id,
            Name = item.Name,
            Description = item.Description,
            Quantity = item.Quantity,
            ListType = item.ListType,
            Source = "adhoc",
        });
    }

    [HttpDelete("{itemId:guid}")]
    public async Task<IActionResult> Delete(Guid jobId, Guid itemId)
    {
        var item = await _db.JobAdhocItems.FirstOrDefaultAsync(ai => ai.Id == itemId && ai.JobId == jobId);
        if (item == null)
            return NotFound(new ErrorResponse { Error = "Ad-hoc item not found" });

        _db.JobAdhocItems.Remove(item);
        await _db.SaveChangesAsync();
        return NoContent();
    }
}
