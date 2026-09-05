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
    [MaxLength(100)]
    public string? Sku { get; set; }
    [MaxLength(100)]
    public string? Category { get; set; }
    [Range(0, 9_999_999)]
    public decimal? Cost { get; set; }
    [Range(-100, 100_000)]
    public decimal? MarkupPercent { get; set; }
    [Range(0, 9_999_999)]
    public decimal? Price { get; set; }
}

// For optional text fields: null = unchanged, empty string = clear.
// Pricing fields (Cost/MarkupPercent/Price) are always applied as sent: null = clear.
public class UpdateInventoryItemRequest
{
    [MaxLength(200)]
    public string? Name { get; set; }
    [MaxLength(10000)]
    public string? Description { get; set; }
    [MaxLength(100)]
    public string? PartNumber { get; set; }
    [MaxLength(100)]
    public string? Sku { get; set; }
    [MaxLength(100)]
    public string? Category { get; set; }
    [Range(0, 9_999_999)]
    public decimal? Cost { get; set; }
    [Range(-100, 100_000)]
    public decimal? MarkupPercent { get; set; }
    [Range(0, 9_999_999)]
    public decimal? Price { get; set; }
}

public class CreateJobInventoryRequest
{
    [Required]
    public Guid InventoryItemId { get; set; }
    [Range(0.01, 100000)]
    public decimal Quantity { get; set; } = 1;
    [Required]
    [RegularExpression("^(used|to_order)$", ErrorMessage = "Invalid list type.")]
    public string ListType { get; set; } = string.Empty;
}

public class UpdateJobInventoryRequest
{
    [Range(0.01, 100000)]
    public decimal? Quantity { get; set; }
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
    [Range(0.01, 100000)]
    public decimal Quantity { get; set; } = 1;
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
    [Range(0.01, 100000)]
    public decimal? Quantity { get; set; }
    [RegularExpression("^(used|to_order)$", ErrorMessage = "Invalid list type.")]
    public string? ListType { get; set; }
    public bool? Ordered { get; set; }
}
