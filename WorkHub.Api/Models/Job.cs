namespace WorkHub.Api.Models;

public class Job
{
    public Guid Id { get; set; }
    public Guid CustomerId { get; set; }
    public Customer Customer { get; set; } = null!;
    public string Title { get; set; } = string.Empty;
    public string Status { get; set; } = "Pending";
    public string Priority { get; set; } = "Normal";
    public string? ScopeNotes { get; set; }
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
