namespace WorkHub.Api.Models;

public class ContactPerson
{
    public Guid Id { get; set; }
    public Guid CustomerId { get; set; }
    public Customer Customer { get; set; } = null!;
    public string Name { get; set; } = string.Empty;
    public string? Role { get; set; } // "Site Super", "Office Manager", etc.
    public string? Phone { get; set; }
    public string? Email { get; set; }
    public DateTime CreatedAt { get; set; }
}
