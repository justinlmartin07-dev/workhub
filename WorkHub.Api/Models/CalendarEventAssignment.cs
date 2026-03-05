namespace WorkHub.Api.Models;

public class CalendarEventAssignment
{
    public Guid Id { get; set; }
    public Guid CalendarEventId { get; set; }
    public CalendarEvent CalendarEvent { get; set; } = null!;
    public Guid UserId { get; set; }
    public User User { get; set; } = null!;
    public DateTime CreatedAt { get; set; }
}
