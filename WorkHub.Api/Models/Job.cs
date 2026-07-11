namespace WorkHub.Api.Models;

public class Job
{
    public Guid Id { get; set; }
    public Guid CustomerId { get; set; }
    public Customer Customer { get; set; } = null!;
    public Guid? MainContactId { get; set; }
    public ContactPerson? MainContact { get; set; }
    public string Title { get; set; } = string.Empty;
    public string Status { get; set; } = "New";
    public string Priority { get; set; } = "Medium";
    public string? ScopeNotes { get; set; }
    public string? Address { get; set; }
    public Guid CreatedBy { get; set; }
    public User CreatedByUser { get; set; } = null!;
    public DateTime CreatedAt { get; set; }
    public DateTime UpdatedAt { get; set; }
    public DateTime? DeletedAt { get; set; }

    public List<JobNote> Notes { get; set; } = [];
    public List<JobPhoto> Photos { get; set; } = [];
    public List<JobInventory> InventoryItems { get; set; } = [];
    public List<JobAdhocItem> AdhocItems { get; set; } = [];
}
