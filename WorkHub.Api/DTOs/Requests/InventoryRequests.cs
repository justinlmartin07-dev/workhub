using System.ComponentModel.DataAnnotations;

namespace WorkHub.Api.DTOs.Requests;

public class CreateInventoryItemRequest
{
    [Required, MaxLength(200)]
    public string Name { get; set; } = string.Empty;
    public string? Description { get; set; }
    [MaxLength(100)]
    public string? PartNumber { get; set; }
}

public class UpdateInventoryItemRequest
{
    [MaxLength(200)]
    public string? Name { get; set; }
    public string? Description { get; set; }
    [MaxLength(100)]
    public string? PartNumber { get; set; }
}

public class CreateJobInventoryRequest
{
    [Required]
    public Guid InventoryItemId { get; set; }
    public int Quantity { get; set; } = 1;
    [Required]
    public string ListType { get; set; } = string.Empty;
}

public class UpdateJobInventoryRequest
{
    public int? Quantity { get; set; }
    public string? ListType { get; set; }
}

public class CreateJobAdhocItemRequest
{
    [Required, MaxLength(200)]
    public string Name { get; set; } = string.Empty;
    public string? Description { get; set; }
    public int Quantity { get; set; } = 1;
    [Required]
    public string ListType { get; set; } = string.Empty;
}

public class UpdateJobAdhocItemRequest
{
    [MaxLength(200)]
    public string? Name { get; set; }
    public string? Description { get; set; }
    public int? Quantity { get; set; }
    public string? ListType { get; set; }
}
