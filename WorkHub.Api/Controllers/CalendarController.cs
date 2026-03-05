using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using WorkHub.Api.Data;
using WorkHub.Api.DTOs.Requests;
using WorkHub.Api.DTOs.Responses;
using WorkHub.Api.Models;

namespace WorkHub.Api.Controllers;

[ApiController]
[Route("v1/events")]
[Authorize]
public class CalendarController : ControllerBase
{
    private readonly WorkHubDbContext _db;

    public CalendarController(WorkHubDbContext db) => _db = db;

    [HttpGet]
    public async Task<IActionResult> List([FromQuery] DateTime from, [FromQuery] DateTime to, [FromQuery] Guid? userId)
    {
        var query = _db.CalendarEvents
            .Where(e => e.StartTime >= from && e.StartTime <= to)
            .Include(e => e.Customer)
            .Include(e => e.Job)
            .Include(e => e.Assignments)
                .ThenInclude(a => a.User);

        if (userId.HasValue)
            query = query.Where(e => e.Assignments.Any(a => a.UserId == userId.Value))
                .Include(e => e.Customer)
                .Include(e => e.Job)
                .Include(e => e.Assignments)
                    .ThenInclude(a => a.User);

        var events = await query
            .OrderBy(e => e.StartTime)
            .Select(e => MapEvent(e))
            .ToListAsync();

        return Ok(events);
    }

    [HttpGet("{id:guid}")]
    public async Task<IActionResult> Get(Guid id)
    {
        var ev = await _db.CalendarEvents
            .Include(e => e.Customer)
            .Include(e => e.Job)
            .Include(e => e.Assignments)
                .ThenInclude(a => a.User)
            .FirstOrDefaultAsync(e => e.Id == id);

        if (ev == null)
            return NotFound(new ErrorResponse { Error = "Event not found" });

        return Ok(MapEvent(ev));
    }

    [HttpPost]
    public async Task<IActionResult> Create([FromBody] CreateEventRequest request)
    {
        var ev = new CalendarEvent
        {
            Id = Guid.NewGuid(),
            Title = request.Title,
            Description = request.Description,
            StartTime = request.StartTime,
            EndTime = request.EndTime,
            ReminderMinutes = request.ReminderMinutes,
            CustomerId = request.CustomerId,
            JobId = request.JobId,
            CreatedBy = this.GetUserId(),
            CreatedAt = DateTime.UtcNow,
        };

        _db.CalendarEvents.Add(ev);

        if (request.AssignedUserIds?.Count > 0)
        {
            foreach (var uid in request.AssignedUserIds)
            {
                _db.CalendarEventAssignments.Add(new CalendarEventAssignment
                {
                    Id = Guid.NewGuid(),
                    CalendarEventId = ev.Id,
                    UserId = uid,
                    CreatedAt = DateTime.UtcNow,
                });
            }
        }

        await _db.SaveChangesAsync();

        // Reload with includes
        var created = await _db.CalendarEvents
            .Include(e => e.Customer)
            .Include(e => e.Job)
            .Include(e => e.Assignments)
                .ThenInclude(a => a.User)
            .FirstAsync(e => e.Id == ev.Id);

        return CreatedAtAction(nameof(Get), new { id = ev.Id }, MapEvent(created));
    }

    [HttpPut("{id:guid}")]
    public async Task<IActionResult> Update(Guid id, [FromBody] UpdateEventRequest request)
    {
        var ev = await _db.CalendarEvents
            .Include(e => e.Customer)
            .Include(e => e.Job)
            .Include(e => e.Assignments)
                .ThenInclude(a => a.User)
            .FirstOrDefaultAsync(e => e.Id == id);

        if (ev == null)
            return NotFound(new ErrorResponse { Error = "Event not found" });

        if (request.Title != null) ev.Title = request.Title;
        if (request.Description != null) ev.Description = request.Description;
        if (request.StartTime.HasValue) ev.StartTime = request.StartTime.Value;
        if (request.EndTime.HasValue) ev.EndTime = request.EndTime.Value;
        if (request.ReminderMinutes.HasValue) ev.ReminderMinutes = request.ReminderMinutes.Value;
        if (request.CustomerId.HasValue) ev.CustomerId = request.CustomerId.Value;
        if (request.JobId.HasValue) ev.JobId = request.JobId.Value;

        await _db.SaveChangesAsync();
        return Ok(MapEvent(ev));
    }

    [HttpDelete("{id:guid}")]
    public async Task<IActionResult> Delete(Guid id)
    {
        var ev = await _db.CalendarEvents.FindAsync(id);
        if (ev == null)
            return NotFound(new ErrorResponse { Error = "Event not found" });

        _db.CalendarEvents.Remove(ev);
        await _db.SaveChangesAsync();
        return NoContent();
    }

    [HttpPost("{id:guid}/assignments")]
    public async Task<IActionResult> AssignUser(Guid id, [FromBody] AssignUserRequest request)
    {
        var ev = await _db.CalendarEvents.FindAsync(id);
        if (ev == null)
            return NotFound(new ErrorResponse { Error = "Event not found" });

        var exists = await _db.CalendarEventAssignments
            .AnyAsync(a => a.CalendarEventId == id && a.UserId == request.UserId);
        if (exists)
            return Conflict(new ErrorResponse { Error = "User already assigned" });

        _db.CalendarEventAssignments.Add(new CalendarEventAssignment
        {
            Id = Guid.NewGuid(),
            CalendarEventId = id,
            UserId = request.UserId,
            CreatedAt = DateTime.UtcNow,
        });

        await _db.SaveChangesAsync();
        return NoContent();
    }

    [HttpDelete("{id:guid}/assignments/{userId:guid}")]
    public async Task<IActionResult> RemoveAssignment(Guid id, Guid userId)
    {
        var assignment = await _db.CalendarEventAssignments
            .FirstOrDefaultAsync(a => a.CalendarEventId == id && a.UserId == userId);
        if (assignment == null)
            return NotFound(new ErrorResponse { Error = "Assignment not found" });

        _db.CalendarEventAssignments.Remove(assignment);
        await _db.SaveChangesAsync();
        return NoContent();
    }

    private static CalendarEventResponse MapEvent(CalendarEvent e) => new()
    {
        Id = e.Id,
        Title = e.Title,
        Description = e.Description,
        StartTime = e.StartTime,
        EndTime = e.EndTime,
        ReminderMinutes = e.ReminderMinutes,
        CustomerId = e.CustomerId,
        CustomerName = e.Customer?.Name,
        JobId = e.JobId,
        JobTitle = e.Job?.Title,
        CreatedBy = e.CreatedBy,
        CreatedAt = e.CreatedAt,
        Assignments = e.Assignments.Select(a => new EventAssignmentResponse
        {
            UserId = a.UserId,
            Name = a.User.Name,
        }).ToList(),
    };
}
