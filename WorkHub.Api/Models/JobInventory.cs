namespace WorkHub.Api.Models;

public class JobInventory
{
    public Guid Id { get; set; }
    public Guid JobId { get; set; }
    public Job Job { get; set; } = null!;
    public Guid InventoryItemId { get; set; }
    public InventoryItem InventoryItem { get; set; } = null!;
    public decimal Quantity { get; set; } = 1; // fractional units allowed (e.g. 1.5 ft)
    public string ListType { get; set; } = string.Empty; // "used" or "to_order"
    public DateTime? OrderedAt { get; set; } // set when a "to_order" item is marked ordered
    public DateTime CreatedAt { get; set; }
    public DateTime? UpdatedAt { get; set; }
}
