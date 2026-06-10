namespace WorkHub.Api.Models;

public class JobAdhocItem
{
    public Guid Id { get; set; }
    public Guid JobId { get; set; }
    public Job Job { get; set; } = null!;
    public string Name { get; set; } = string.Empty;
    public string? Description { get; set; }
    public int Quantity { get; set; } = 1;
    public string ListType { get; set; } = string.Empty; // "used" or "to_order"
    public DateTime? OrderedAt { get; set; } // set when a "to_order" item is marked ordered
    public DateTime CreatedAt { get; set; }
    public DateTime? UpdatedAt { get; set; }
}
