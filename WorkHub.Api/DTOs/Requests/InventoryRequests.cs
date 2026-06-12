using System.ComponentModel.DataAnnotations;

namespace WorkHub.Api.DTOs.Requests;

public class CreateInventoryItemRequest
{
    [Required, MaxLength(200)]
    public string Name { get; set; } = string.Empty;
    [MaxLength(10000)]
    public string? Description { get; set; }
    [MaxLength(100)]
    public string? PartNumber { get; set; }
}

public class UpdateInventoryItemRequest
{
    [MaxLength(200)]
    public string? Name { get; set; }
    [MaxLength(10000)]
    public string? Description { get; set; }
    [MaxLength(100)]
    public string? PartNumber { get; set; }
}

public class CreateJobInventoryRequest
{
    [Required]
    public Guid InventoryItemId { get; set; }
    [Range(1, 100000)]
    public int Quantity { get; set; } = 1;
    [Required]
    [RegularExpression("^(used|to_order)$", ErrorMessage = "Invalid list type.")]
    public string ListType { get; set; } = string.Empty;
}

public class UpdateJobInventoryRequest
{
    [Range(1, 100000)]
    public int? Quantity { get; set; }
    [RegularExpression("^(used|to_order)$", ErrorMessage = "Invalid list type.")]
    public string? ListType { get; set; }
    public bool? Ordered { get; set; }
}

public class CreateJobAdhocItemRequest
{
    [Required, MaxLength(200)]
    public string Name { get; set; } = string.Empty;
    [MaxLength(10000)]
    public string? Description { get; set; }
    [Range(1, 100000)]
    public int Quantity { get; set; } = 1;
    [Required]
    [RegularExpression("^(used|to_order)$", ErrorMessage = "Invalid list type.")]
    public string ListType { get; set; } = string.Empty;
}

public class UpdateJobAdhocItemRequest
{
    [MaxLength(200)]
    public string? Name { get; set; }
    [MaxLength(10000)]
    public string? Description { get; set; }
    [Range(1, 100000)]
    public int? Quantity { get; set; }
    [RegularExpression("^(used|to_order)$", ErrorMessage = "Invalid list type.")]
    public string? ListType { get; set; }
    public bool? Ordered { get; set; }
}
