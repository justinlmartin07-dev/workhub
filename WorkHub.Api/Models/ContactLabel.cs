namespace WorkHub.Api.Models;

public class ContactLabel
{
    public Guid Id { get; set; }
    public string Type { get; set; } = string.Empty; // "phone" or "email"
    public string Label { get; set; } = string.Empty;
    public int SortOrder { get; set; }
    public DateTime CreatedAt { get; set; }
}
