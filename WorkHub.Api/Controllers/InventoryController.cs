using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using WorkHub.Api.Data;
using WorkHub.Api.DTOs.Requests;
using WorkHub.Api.DTOs.Responses;
using WorkHub.Api.Models;

namespace WorkHub.Api.Controllers;

[ApiController]
[Route("v1/inventory")]
[Authorize]
public class InventoryController : ControllerBase
{
    private readonly WorkHubDbContext _db;

    public InventoryController(WorkHubDbContext db) => _db = db;

    [HttpGet]
    public async Task<IActionResult> List([FromQuery] string? q, [FromQuery] int page = 1, [FromQuery] int pageSize = 25)
    {
        pageSize = Math.Clamp(pageSize, 1, 100);
        page = Math.Max(page, 1);

        var query = _db.InventoryItems.AsQueryable();

        if (!string.IsNullOrWhiteSpace(q))
            query = query.Where(i => EF.Functions.ILike(i.Name, $"%{q}%")
                || (i.PartNumber != null && EF.Functions.ILike(i.PartNumber, $"%{q}%")));

        var totalCount = await query.CountAsync();
        var items = await query
            .OrderBy(i => i.Name)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .Select(i => new InventoryItemResponse
            {
                Id = i.Id,
                Name = i.Name,
                Description = i.Description,
                PartNumber = i.PartNumber,
                CreatedAt = i.CreatedAt,
                UpdatedAt = i.UpdatedAt,
            })
            .ToListAsync();

        return Ok(new PagedResponse<InventoryItemResponse>
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
        var item = await _db.InventoryItems.FindAsync(id);
        if (item == null)
            return NotFound(new ErrorResponse { Error = "Inventory item not found" });

        return Ok(new InventoryItemResponse
        {
            Id = item.Id,
            Name = item.Name,
            Description = item.Description,
            PartNumber = item.PartNumber,
            CreatedAt = item.CreatedAt,
            UpdatedAt = item.UpdatedAt,
        });
    }

    [HttpPost]
    public async Task<IActionResult> Create([FromBody] CreateInventoryItemRequest request)
    {
        var item = new InventoryItem
        {
            Id = Guid.NewGuid(),
            Name = request.Name,
            Description = request.Description,
            PartNumber = request.PartNumber,
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow,
        };

        _db.InventoryItems.Add(item);
        await _db.SaveChangesAsync();

        return CreatedAtAction(nameof(Get), new { id = item.Id }, new InventoryItemResponse
        {
            Id = item.Id,
            Name = item.Name,
            Description = item.Description,
            PartNumber = item.PartNumber,
            CreatedAt = item.CreatedAt,
            UpdatedAt = item.UpdatedAt,
        });
    }

    [HttpPut("{id:guid}")]
    public async Task<IActionResult> Update(Guid id, [FromBody] UpdateInventoryItemRequest request)
    {
        var item = await _db.InventoryItems.FindAsync(id);
        if (item == null)
            return NotFound(new ErrorResponse { Error = "Inventory item not found" });

        if (request.Name != null) item.Name = request.Name;
        if (request.Description != null) item.Description = request.Description;
        if (request.PartNumber != null) item.PartNumber = request.PartNumber;
        item.UpdatedAt = DateTime.UtcNow;

        await _db.SaveChangesAsync();

        return Ok(new InventoryItemResponse
        {
            Id = item.Id,
            Name = item.Name,
            Description = item.Description,
            PartNumber = item.PartNumber,
            CreatedAt = item.CreatedAt,
            UpdatedAt = item.UpdatedAt,
        });
    }

    [HttpDelete("{id:guid}")]
    public async Task<IActionResult> Delete(Guid id)
    {
        var item = await _db.InventoryItems
            .Include(i => i.JobInventories)
                .ThenInclude(ji => ji.Job)
            .FirstOrDefaultAsync(i => i.Id == id);

        if (item == null)
            return NotFound(new ErrorResponse { Error = "Inventory item not found" });

        var referencingJobs = item.JobInventories
            .Where(ji => ji.Job.DeletedAt == null)
            .Select(ji => ji.Job)
            .Distinct()
            .ToList();

        if (referencingJobs.Any())
        {
            return Conflict(new ErrorResponse
            {
                Error = "Cannot delete inventory item referenced by jobs",
                Details = new
                {
                    referencingJobs = referencingJobs.Select(j => new { j.Id, j.Title })
                }
            });
        }

        _db.InventoryItems.Remove(item);
        await _db.SaveChangesAsync();
        return NoContent();
    }
}
