namespace WorkHub.Api.Models;

public class JobInventory
{
    public Guid Id { get; set; }
    public Guid JobId { get; set; }
    public Job Job { get; set; } = null!;
    public Guid InventoryItemId { get; set; }
    public InventoryItem InventoryItem { get; set; } = null!;
    public int Quantity { get; set; } = 1;
    public string ListType { get; set; } = string.Empty; // "used" or "to_order"
    public DateTime CreatedAt { get; set; }
    public DateTime? UpdatedAt { get; set; }
}
