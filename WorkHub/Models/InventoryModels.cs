namespace WorkHub.Models;

public class InventoryItemResponse
{
    public Guid Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public string? Description { get; set; }
    public string? PartNumber { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime UpdatedAt { get; set; }
}

public class CreateInventoryItemRequest
{
    public string Name { get; set; } = string.Empty;
    public string? Description { get; set; }
    public string? PartNumber { get; set; }
}

public class UpdateInventoryItemRequest
{
    public string? Name { get; set; }
    public string? Description { get; set; }
    public string? PartNumber { get; set; }
}
