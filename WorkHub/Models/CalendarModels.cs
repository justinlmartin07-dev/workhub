namespace WorkHub.Models;

public class CalendarEventResponse
{
    public Guid Id { get; set; }
    public string Title { get; set; } = string.Empty;
    public string? Description { get; set; }
    public DateTime StartTime { get; set; }
    public DateTime? EndTime { get; set; }
    public int? ReminderMinutes { get; set; }
    public Guid? CustomerId { get; set; }
    public string? CustomerName { get; set; }
    public Guid? JobId { get; set; }
    public string? JobTitle { get; set; }
    public Guid CreatedBy { get; set; }
    public DateTime CreatedAt { get; set; }
    public List<EventAssignmentResponse> Assignments { get; set; } = new();
}

public class EventAssignmentResponse
{
    public Guid UserId { get; set; }
    public string Name { get; set; } = string.Empty;
}

public class CreateEventRequest
{
    public string Title { get; set; } = string.Empty;
    public string? Description { get; set; }
    public DateTime StartTime { get; set; }
    public DateTime? EndTime { get; set; }
    public int? ReminderMinutes { get; set; }
    public Guid? CustomerId { get; set; }
    public Guid? JobId { get; set; }
    public List<Guid>? AssignedUserIds { get; set; }
}

public class UpdateEventRequest
{
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
    public Guid UserId { get; set; }
}
