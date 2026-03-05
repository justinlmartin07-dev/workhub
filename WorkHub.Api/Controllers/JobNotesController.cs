using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using WorkHub.Api.Data;
using WorkHub.Api.DTOs.Requests;
using WorkHub.Api.DTOs.Responses;
using WorkHub.Api.Models;

namespace WorkHub.Api.Controllers;

[ApiController]
[Route("v1/jobs/{jobId:guid}/notes")]
[Authorize]
public class JobNotesController : ControllerBase
{
    private readonly WorkHubDbContext _db;

    public JobNotesController(WorkHubDbContext db) => _db = db;

    [HttpGet]
    public async Task<IActionResult> List(Guid jobId)
    {
        var jobExists = await _db.Jobs.AnyAsync(j => j.Id == jobId && j.DeletedAt == null);
        if (!jobExists)
            return NotFound(new ErrorResponse { Error = "Job not found" });

        var notes = await _db.JobNotes
            .Where(n => n.JobId == jobId)
            .OrderBy(n => n.CreatedAt)
            .Include(n => n.CreatedByUser)
            .Select(n => new JobNoteResponse
            {
                Id = n.Id,
                Content = n.Content,
                CreatedBy = n.CreatedBy,
                CreatedByName = n.CreatedByUser.Name,
                CreatedAt = n.CreatedAt,
                UpdatedAt = n.UpdatedAt,
            })
            .ToListAsync();

        return Ok(notes);
    }

    [HttpPost]
    public async Task<IActionResult> Create(Guid jobId, [FromBody] CreateJobNoteRequest request)
    {
        var jobExists = await _db.Jobs.AnyAsync(j => j.Id == jobId && j.DeletedAt == null);
        if (!jobExists)
            return NotFound(new ErrorResponse { Error = "Job not found" });

        var userId = this.GetUserId();
        var user = await _db.Users.FindAsync(userId);

        var note = new JobNote
        {
            Id = Guid.NewGuid(),
            JobId = jobId,
            Content = request.Content,
            CreatedBy = userId,
            CreatedAt = DateTime.UtcNow,
        };

        _db.JobNotes.Add(note);
        await _db.SaveChangesAsync();

        return Created($"v1/jobs/{jobId}/notes/{note.Id}", new JobNoteResponse
        {
            Id = note.Id,
            Content = note.Content,
            CreatedBy = note.CreatedBy,
            CreatedByName = user!.Name,
            CreatedAt = note.CreatedAt,
            UpdatedAt = note.UpdatedAt,
        });
    }

    [HttpPut("{noteId:guid}")]
    public async Task<IActionResult> Update(Guid jobId, Guid noteId, [FromBody] UpdateJobNoteRequest request)
    {
        var note = await _db.JobNotes.Include(n => n.CreatedByUser)
            .FirstOrDefaultAsync(n => n.Id == noteId && n.JobId == jobId);
        if (note == null)
            return NotFound(new ErrorResponse { Error = "Note not found" });

        note.Content = request.Content;
        note.UpdatedAt = DateTime.UtcNow;
        await _db.SaveChangesAsync();

        return Ok(new JobNoteResponse
        {
            Id = note.Id,
            Content = note.Content,
            CreatedBy = note.CreatedBy,
            CreatedByName = note.CreatedByUser.Name,
            CreatedAt = note.CreatedAt,
            UpdatedAt = note.UpdatedAt,
        });
    }

    [HttpDelete("{noteId:guid}")]
    public async Task<IActionResult> Delete(Guid jobId, Guid noteId)
    {
        var note = await _db.JobNotes.FirstOrDefaultAsync(n => n.Id == noteId && n.JobId == jobId);
        if (note == null)
            return NotFound(new ErrorResponse { Error = "Note not found" });

        _db.JobNotes.Remove(note);
        await _db.SaveChangesAsync();
        return NoContent();
    }
}
