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
    public async Task<IActionResult> List([FromQuery] string? q, [FromQuery] string? category, [FromQuery] int page = 1, [FromQuery] int pageSize = 25)
    {
        pageSize = Math.Clamp(pageSize, 1, 100);
        page = Math.Max(page, 1);

        var query = _db.InventoryItems.AsQueryable();

        if (!string.IsNullOrWhiteSpace(q))
            query = query.Where(i => EF.Functions.ILike(i.Name, $"%{q}%")
                || (i.PartNumber != null && EF.Functions.ILike(i.PartNumber, $"%{q}%"))
                || (i.Sku != null && EF.Functions.ILike(i.Sku, $"%{q}%"))
                || (i.Category != null && EF.Functions.ILike(i.Category, $"%{q}%")));

        if (!string.IsNullOrWhiteSpace(category))
            query = query.Where(i => i.Category != null && EF.Functions.ILike(i.Category, category));

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
                Sku = i.Sku,
                Category = i.Category,
                Cost = i.Cost,
                MarkupPercent = i.MarkupPercent,
                Price = i.Price,
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

    [HttpGet("categories")]
    public async Task<IActionResult> Categories()
    {
        var categories = await _db.InventoryItems
            .Where(i => i.Category != null)
            .Select(i => i.Category!)
            .Distinct()
            .OrderBy(c => c)
            .ToListAsync();

        return Ok(categories);
    }

    [HttpGet("markups")]
    public async Task<IActionResult> Markups()
    {
        var markups = await _db.InventoryItems
            .Where(i => i.MarkupPercent != null)
            .Select(i => i.MarkupPercent!.Value)
            .Distinct()
            .OrderBy(m => m)
            .ToListAsync();

        return Ok(markups);
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
            Sku = item.Sku,
            Category = item.Category,
            Cost = item.Cost,
            MarkupPercent = item.MarkupPercent,
            Price = item.Price,
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
            Sku = request.Sku,
            Category = NormalizeOptional(request.Category),
            Cost = request.Cost,
            MarkupPercent = request.MarkupPercent,
            Price = request.Price,
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
            Sku = item.Sku,
            Category = item.Category,
            Cost = item.Cost,
            MarkupPercent = item.MarkupPercent,
            Price = item.Price,
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

        // For optional text fields: null = unchanged, empty string = clear.
        if (request.Name != null) item.Name = request.Name;
        if (request.Description != null) item.Description = NormalizeOptional(request.Description);
        if (request.PartNumber != null) item.PartNumber = NormalizeOptional(request.PartNumber);
        if (request.Sku != null) item.Sku = NormalizeOptional(request.Sku);
        if (request.Category != null) item.Category = NormalizeOptional(request.Category);
        // Pricing fields are always applied as sent: null = clear.
        item.Cost = request.Cost;
        item.MarkupPercent = request.MarkupPercent;
        item.Price = request.Price;
        item.UpdatedAt = DateTime.UtcNow;

        await _db.SaveChangesAsync();

        return Ok(new InventoryItemResponse
        {
            Id = item.Id,
            Name = item.Name,
            Description = item.Description,
            PartNumber = item.PartNumber,
            Sku = item.Sku,
            Category = item.Category,
            Cost = item.Cost,
            MarkupPercent = item.MarkupPercent,
            Price = item.Price,
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

    private static string? NormalizeOptional(string? value)
    {
        var trimmed = value?.Trim();
        return string.IsNullOrEmpty(trimmed) ? null : trimmed;
    }
}
