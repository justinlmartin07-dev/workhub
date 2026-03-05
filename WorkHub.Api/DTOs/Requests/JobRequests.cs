using System.ComponentModel.DataAnnotations;

namespace WorkHub.Api.DTOs.Requests;

public class CreateJobRequest
{
    [Required]
    public Guid CustomerId { get; set; }
    [Required, MaxLength(200)]
    public string Title { get; set; } = string.Empty;
    [MaxLength(50)]
    public string? Status { get; set; }
    [MaxLength(50)]
    public string? Priority { get; set; }
    public string? ScopeNotes { get; set; }
}

public class UpdateJobRequest
{
    [MaxLength(200)]
    public string? Title { get; set; }
    [MaxLength(50)]
    public string? Status { get; set; }
    [MaxLength(50)]
    public string? Priority { get; set; }
    public string? ScopeNotes { get; set; }
}

public class CreateJobNoteRequest
{
    [Required]
    public string Content { get; set; } = string.Empty;
}

public class UpdateJobNoteRequest
{
    [Required]
    public string Content { get; set; } = string.Empty;
}
