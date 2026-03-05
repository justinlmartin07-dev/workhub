using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using WorkHub.Api.Data;
using WorkHub.Api.DTOs.Requests;
using WorkHub.Api.DTOs.Responses;
using WorkHub.Api.Models;

namespace WorkHub.Api.Controllers;

[ApiController]
[Route("v1/jobs/{jobId:guid}/items")]
[Authorize]
public class JobItemsController : ControllerBase
{
    private readonly WorkHubDbContext _db;

    public JobItemsController(WorkHubDbContext db) => _db = db;

    [HttpGet]
    public async Task<IActionResult> List(Guid jobId, [FromQuery(Name = "list_type")] string? listType)
    {
        var jobExists = await _db.Jobs.AnyAsync(j => j.Id == jobId && j.DeletedAt == null);
        if (!jobExists)
            return NotFound(new ErrorResponse { Error = "Job not found" });

        var query = _db.JobInventories
            .Where(ji => ji.JobId == jobId)
            .Include(ji => ji.InventoryItem);

        if (!string.IsNullOrWhiteSpace(listType))
            query = query.Where(ji => ji.ListType == listType).Include(ji => ji.InventoryItem);

        var items = await query
            .Select(ji => new JobItemResponse
            {
                Id = ji.Id,
                Name = ji.InventoryItem.Name,
                Description = ji.InventoryItem.Description,
                PartNumber = ji.InventoryItem.PartNumber,
                Quantity = ji.Quantity,
                ListType = ji.ListType,
                Source = "library",
                InventoryItemId = ji.InventoryItemId,
            })
            .ToListAsync();

        return Ok(items);
    }

    [HttpPost]
    public async Task<IActionResult> Create(Guid jobId, [FromBody] CreateJobInventoryRequest request)
    {
        var jobExists = await _db.Jobs.AnyAsync(j => j.Id == jobId && j.DeletedAt == null);
        if (!jobExists)
            return NotFound(new ErrorResponse { Error = "Job not found" });

        var inventoryItem = await _db.InventoryItems.FindAsync(request.InventoryItemId);
        if (inventoryItem == null)
            return BadRequest(new ErrorResponse { Error = "Inventory item not found" });

        var ji = new JobInventory
        {
            Id = Guid.NewGuid(),
            JobId = jobId,
            InventoryItemId = request.InventoryItemId,
            Quantity = request.Quantity,
            ListType = request.ListType,
            CreatedAt = DateTime.UtcNow,
        };

        _db.JobInventories.Add(ji);
        await _db.SaveChangesAsync();

        return Created($"v1/jobs/{jobId}/items/{ji.Id}", new JobItemResponse
        {
            Id = ji.Id,
            Name = inventoryItem.Name,
            Description = inventoryItem.Description,
            PartNumber = inventoryItem.PartNumber,
            Quantity = ji.Quantity,
            ListType = ji.ListType,
            Source = "library",
            InventoryItemId = ji.InventoryItemId,
        });
    }

    [HttpPut("{itemId:guid}")]
    public async Task<IActionResult> Update(Guid jobId, Guid itemId, [FromBody] UpdateJobInventoryRequest request)
    {
        var ji = await _db.JobInventories
            .Include(ji => ji.InventoryItem)
            .FirstOrDefaultAsync(ji => ji.Id == itemId && ji.JobId == jobId);

        if (ji == null)
            return NotFound(new ErrorResponse { Error = "Job item not found" });

        if (request.Quantity.HasValue) ji.Quantity = request.Quantity.Value;
        if (request.ListType != null) ji.ListType = request.ListType;
        ji.UpdatedAt = DateTime.UtcNow;

        await _db.SaveChangesAsync();

        return Ok(new JobItemResponse
        {
            Id = ji.Id,
            Name = ji.InventoryItem.Name,
            Description = ji.InventoryItem.Description,
            PartNumber = ji.InventoryItem.PartNumber,
            Quantity = ji.Quantity,
            ListType = ji.ListType,
            Source = "library",
            InventoryItemId = ji.InventoryItemId,
        });
    }

    [HttpDelete("{itemId:guid}")]
    public async Task<IActionResult> Delete(Guid jobId, Guid itemId)
    {
        var ji = await _db.JobInventories.FirstOrDefaultAsync(ji => ji.Id == itemId && ji.JobId == jobId);
        if (ji == null)
            return NotFound(new ErrorResponse { Error = "Job item not found" });

        _db.JobInventories.Remove(ji);
        await _db.SaveChangesAsync();
        return NoContent();
    }
}
