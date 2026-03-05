using System.ComponentModel.DataAnnotations;

namespace WorkHub.Api.DTOs.Requests;

public class CreateEventRequest
{
    [Required, MaxLength(200)]
    public string Title { get; set; } = string.Empty;
    public string? Description { get; set; }
    [Required]
    public DateTime StartTime { get; set; }
    public DateTime? EndTime { get; set; }
    public int? ReminderMinutes { get; set; }
    public Guid? CustomerId { get; set; }
    public Guid? JobId { get; set; }
    public List<Guid>? AssignedUserIds { get; set; }
}

public class UpdateEventRequest
{
    [MaxLength(200)]
    public string? Title { get; set; }
    public string? Description { get; set; }
    public DateTime? StartTime { get; set; }
    public DateTime? EndTime { get; set; }
    public int? ReminderMinutes { get; set; }
    public Guid? CustomerId { get; set; }
    public Guid? JobId { get; set; }
}

public class AssignUserRequest
{
    [Required]
    public Guid UserId { get; set; }
}

public class UpdateProfileRequest
{
    [Required, MaxLength(200)]
    public string Name { get; set; } = string.Empty;
}
