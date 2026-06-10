using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using WorkHub.Api.Data;
using WorkHub.Api.DTOs.Responses;

namespace WorkHub.Api.Controllers;

[ApiController]
[Route("v1/orders")]
[Authorize]
public class OrdersController : ControllerBase
{
    private readonly WorkHubDbContext _db;

    public OrdersController(WorkHubDbContext db) => _db = db;

    // All "to_order" parts across active (non-deleted, not Complete) jobs.
    [HttpGet]
    public async Task<IActionResult> List()
    {
        var library = await _db.JobInventories
            .Where(ji => ji.ListType == "to_order"
                && ji.Job.DeletedAt == null
                && ji.Job.Status != "Complete")
            .Select(ji => new OrderLineResponse
            {
                Id = ji.Id,
                Source = "library",
                Name = ji.InventoryItem.Name,
                Description = ji.InventoryItem.Description,
                PartNumber = ji.InventoryItem.PartNumber,
                Quantity = ji.Quantity,
                OrderedAt = ji.OrderedAt,
                JobId = ji.JobId,
                JobTitle = ji.Job.Title,
                CustomerId = ji.Job.CustomerId,
                CustomerName = ji.Job.Customer.Name,
            })
            .ToListAsync();

        var adhoc = await _db.JobAdhocItems
            .Where(ai => ai.ListType == "to_order"
                && ai.Job.DeletedAt == null
                && ai.Job.Status != "Complete")
            .Select(ai => new OrderLineResponse
            {
                Id = ai.Id,
                Source = "adhoc",
                Name = ai.Name,
                Description = ai.Description,
                PartNumber = null,
                Quantity = ai.Quantity,
                OrderedAt = ai.OrderedAt,
                JobId = ai.JobId,
                JobTitle = ai.Job.Title,
                CustomerId = ai.Job.CustomerId,
                CustomerName = ai.Job.Customer.Name,
            })
            .ToListAsync();

        var all = library
            .Concat(adhoc)
            .OrderBy(o => o.OrderedAt.HasValue) // outstanding first, ordered last
            .ThenBy(o => o.CustomerName)
            .ThenBy(o => o.JobTitle)
            .ThenBy(o => o.Name)
            .ToList();

        return Ok(all);
    }
}
