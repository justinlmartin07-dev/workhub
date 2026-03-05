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
[Route("v1/customers")]
[Authorize]
public class CustomersController : ControllerBase
{
    private readonly WorkHubDbContext _db;
    private readonly PhotoService _photos;

    public CustomersController(WorkHubDbContext db, PhotoService photos)
    {
        _db = db;
        _photos = photos;
    }

    [HttpGet]
    public async Task<IActionResult> List([FromQuery] string? q, [FromQuery] int page = 1, [FromQuery] int pageSize = 25)
    {
        pageSize = Math.Clamp(pageSize, 1, 100);
        page = Math.Max(page, 1);

        var query = _db.Customers
            .Where(c => c.DeletedAt == null);

        if (!string.IsNullOrWhiteSpace(q))
            query = query.Where(c => EF.Functions.ILike(c.Name, $"%{q}%")
                || (c.Phone != null && EF.Functions.ILike(c.Phone, $"%{q}%"))
                || (c.Email != null && EF.Functions.ILike(c.Email, $"%{q}%")));

        var totalCount = await query.CountAsync();
        var items = await query
            .OrderBy(c => c.Name)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .Include(c => c.Jobs.Where(j => j.DeletedAt == null).OrderByDescending(j => j.CreatedAt).Take(1))
            .Select(c => new CustomerResponse
            {
                Id = c.Id,
                Name = c.Name,
                Phone = c.Phone,
                Email = c.Email,
                Address = c.Address,
                Notes = c.Notes,
                CreatedAt = c.CreatedAt,
                UpdatedAt = c.UpdatedAt,
                Jobs = c.Jobs.Where(j => j.DeletedAt == null).OrderByDescending(j => j.CreatedAt).Take(1)
                    .Select(j => new JobBriefResponse { Id = j.Id, Title = j.Title, Status = j.Status, Priority = j.Priority }).ToList(),
            })
            .ToListAsync();

        return Ok(new PagedResponse<CustomerResponse>
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
        var customer = await _db.Customers
            .Where(c => c.Id == id && c.DeletedAt == null)
            .Include(c => c.Photos.OrderByDescending(p => p.UploadedAt))
            .Include(c => c.Jobs.Where(j => j.DeletedAt == null).OrderByDescending(j => j.CreatedAt))
            .FirstOrDefaultAsync();

        if (customer == null)
            return NotFound(new ErrorResponse { Error = "Customer not found" });

        return Ok(new CustomerResponse
        {
            Id = customer.Id,
            Name = customer.Name,
            Phone = customer.Phone,
            Email = customer.Email,
            Address = customer.Address,
            Notes = customer.Notes,
            CreatedAt = customer.CreatedAt,
            UpdatedAt = customer.UpdatedAt,
            Photos = customer.Photos.Select(p => new PhotoResponse
            {
                Id = p.Id,
                Url = _photos.GeneratePresignedUrl(p.R2ObjectKey),
                FileName = p.FileName,
                UploadedAt = p.UploadedAt,
            }).ToList(),
            Jobs = customer.Jobs.Select(j => new JobBriefResponse
            {
                Id = j.Id,
                Title = j.Title,
                Status = j.Status,
                Priority = j.Priority,
            }).ToList(),
        });
    }

    [HttpPost]
    public async Task<IActionResult> Create([FromBody] CreateCustomerRequest request)
    {
        var customer = new Customer
        {
            Id = Guid.NewGuid(),
            Name = request.Name,
            Phone = request.Phone,
            Email = request.Email,
            Address = request.Address,
            NormalizedAddress = AddressNormalizer.Normalize(request.Address),
            Notes = request.Notes,
            CreatedBy = this.GetUserId(),
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow,
        };

        _db.Customers.Add(customer);
        await _db.SaveChangesAsync();

        return CreatedAtAction(nameof(Get), new { id = customer.Id }, new CustomerResponse
        {
            Id = customer.Id,
            Name = customer.Name,
            Phone = customer.Phone,
            Email = customer.Email,
            Address = customer.Address,
            Notes = customer.Notes,
            CreatedAt = customer.CreatedAt,
            UpdatedAt = customer.UpdatedAt,
        });
    }

    [HttpPut("{id:guid}")]
    public async Task<IActionResult> Update(Guid id, [FromBody] UpdateCustomerRequest request)
    {
        var customer = await _db.Customers.FirstOrDefaultAsync(c => c.Id == id && c.DeletedAt == null);
        if (customer == null)
            return NotFound(new ErrorResponse { Error = "Customer not found" });

        if (request.Name != null) customer.Name = request.Name;
        if (request.Phone != null) customer.Phone = request.Phone;
        if (request.Email != null) customer.Email = request.Email;
        if (request.Notes != null) customer.Notes = request.Notes;
        if (request.Address != null)
        {
            customer.Address = request.Address;
            customer.NormalizedAddress = AddressNormalizer.Normalize(request.Address);
        }
        customer.UpdatedAt = DateTime.UtcNow;

        await _db.SaveChangesAsync();

        return Ok(new CustomerResponse
        {
            Id = customer.Id,
            Name = customer.Name,
            Phone = customer.Phone,
            Email = customer.Email,
            Address = customer.Address,
            Notes = customer.Notes,
            CreatedAt = customer.CreatedAt,
            UpdatedAt = customer.UpdatedAt,
        });
    }

    [HttpDelete("{id:guid}")]
    public async Task<IActionResult> Delete(Guid id)
    {
        var customer = await _db.Customers
            .Include(c => c.Jobs.Where(j => j.DeletedAt == null))
            .FirstOrDefaultAsync(c => c.Id == id && c.DeletedAt == null);

        if (customer == null)
            return NotFound(new ErrorResponse { Error = "Customer not found" });

        var blockingJobs = customer.Jobs.Where(j => j.Status != "Complete").ToList();
        if (blockingJobs.Any())
        {
            return Conflict(new ErrorResponse
            {
                Error = "Cannot delete customer with active jobs",
                Details = new
                {
                    blockingJobs = blockingJobs.Select(j => new { j.Id, j.Title, j.Status })
                }
            });
        }

        // Soft-delete customer and cascade to complete jobs
        customer.DeletedAt = DateTime.UtcNow;
        foreach (var job in customer.Jobs)
            job.DeletedAt = DateTime.UtcNow;

        await _db.SaveChangesAsync();
        return NoContent();
    }
}
