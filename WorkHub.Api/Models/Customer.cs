namespace WorkHub.Api.Models;

public class Customer
{
    public Guid Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public string? Address { get; set; }
    public string? NormalizedAddress { get; set; }
    public string? Notes { get; set; }
    public Guid CreatedBy { get; set; }
    public User CreatedByUser { get; set; } = null!;
    public DateTime CreatedAt { get; set; }
    public DateTime UpdatedAt { get; set; }
    public DateTime? DeletedAt { get; set; }

    public List<CustomerContact> Contacts { get; set; } = [];
    public List<Job> Jobs { get; set; } = [];
    public List<CustomerPhoto> Photos { get; set; } = [];
}
