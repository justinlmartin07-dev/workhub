namespace WorkHub.Api.DTOs.Responses;

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
    public List<EventAssignmentResponse> Assignments { get; set; } = [];
}

public class EventAssignmentResponse
{
    public Guid UserId { get; set; }
    public string Name { get; set; } = string.Empty;
}
