namespace WorkHub.Api.Models;

public class CustomerContact
{
    public Guid Id { get; set; }
    public Guid CustomerId { get; set; }
    public Customer Customer { get; set; } = null!;
    public string Type { get; set; } = string.Empty; // "phone" or "email"
    public string Label { get; set; } = string.Empty; // "Mobile", "Home", "Work", etc.
    public string Value { get; set; } = string.Empty;
    public bool IsPrimary { get; set; }
    public DateTime CreatedAt { get; set; }
}
