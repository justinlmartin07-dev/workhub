using System.ComponentModel.DataAnnotations;

namespace WorkHub.Api.DTOs.Requests;

public class CreateJobRequest
{
    [Required]
    public Guid CustomerId { get; set; }
    [Required, MaxLength(200)]
    public string Title { get; set; } = string.Empty;
    [MaxLength(50)]
    [RegularExpression("^(New|In Progress|On Hold|Complete|Billed|Cancelled)$", ErrorMessage = "Invalid status.")]
    public string? Status { get; set; }
    [MaxLength(50)]
    public string? Priority { get; set; }
    [MaxLength(10000)]
    public string? ScopeNotes { get; set; }
    [MaxLength(500)]
    public string? Address { get; set; }
    public Guid? MainContactId { get; set; }
}

public class UpdateJobRequest
{
    [MaxLength(200)]
    public string? Title { get; set; }
    [MaxLength(50)]
    [RegularExpression("^(New|In Progress|On Hold|Complete|Billed|Cancelled)$", ErrorMessage = "Invalid status.")]
    public string? Status { get; set; }
    [MaxLength(50)]
    public string? Priority { get; set; }
    [MaxLength(10000)]
    public string? ScopeNotes { get; set; }
    [MaxLength(500)]
    public string? Address { get; set; }
    // Null = don't change; Guid.Empty = clear (the client always sends the field)
    public Guid? MainContactId { get; set; }
}

public class CreateJobNoteRequest
{
    [Required, MaxLength(10000)]
    public string Content { get; set; } = string.Empty;
}

public class UpdateJobNoteRequest
{
    [Required, MaxLength(10000)]
    public string Content { get; set; } = string.Empty;
}
