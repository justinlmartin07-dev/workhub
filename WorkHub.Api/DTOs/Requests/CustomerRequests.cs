using System.ComponentModel.DataAnnotations;

namespace WorkHub.Api.DTOs.Requests;

public class CreateCustomerRequest
{
    [Required, MaxLength(200)]
    public string Name { get; set; } = string.Empty;
    [MaxLength(200)]
    public string? CompanyName { get; set; }
    [MaxLength(500)]
    public string? Address { get; set; }
    [MaxLength(10000)]
    public string? Notes { get; set; }
    public List<CustomerContactRequest>? Contacts { get; set; }
}

public class UpdateCustomerRequest
{
    [MaxLength(200)]
    public string? Name { get; set; }
    [MaxLength(200)]
    public string? CompanyName { get; set; }
    [MaxLength(500)]
    public string? Address { get; set; }
    [MaxLength(10000)]
    public string? Notes { get; set; }
    public List<CustomerContactRequest>? Contacts { get; set; }
}

public class CustomerContactRequest
{
    [Required, MaxLength(20)]
    public string Type { get; set; } = string.Empty;
    [MaxLength(50)]
    public string Label { get; set; } = string.Empty;
    [Required, MaxLength(200)]
    public string Value { get; set; } = string.Empty;
    public bool IsPrimary { get; set; }
}
