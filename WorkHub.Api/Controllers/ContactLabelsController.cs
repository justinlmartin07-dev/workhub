using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using WorkHub.Api.Data;

namespace WorkHub.Api.Controllers;

[ApiController]
[Route("v1/contact-labels")]
[Authorize]
public class ContactLabelsController : ControllerBase
{
    private readonly WorkHubDbContext _db;

    public ContactLabelsController(WorkHubDbContext db) => _db = db;

    [HttpGet]
    public async Task<IActionResult> GetAll()
    {
        var labels = await _db.ContactLabels
            .OrderBy(l => l.Type)
            .ThenBy(l => l.SortOrder)
            .Select(l => new { l.Type, l.Label })
            .ToListAsync();

        return Ok(labels);
    }
}
