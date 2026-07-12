using System.ComponentModel.DataAnnotations;

namespace WorkHub.Api.DTOs.Requests;

public class CreateCustomerRequest
{
    [Required, MaxLength(200)]
    public string Name { get; set; } = string.Empty;
    [MaxLength(500)]
    public string? Address { get; set; }
    [MaxLength(10000)]
    public string? Notes { get; set; }
    public List<CustomerContactRequest>? Contacts { get; set; }
    public List<ContactPersonRequest>? Persons { get; set; }
}

public class UpdateCustomerRequest
{
    [MaxLength(200)]
    public string? Name { get; set; }
    [MaxLength(500)]
    public string? Address { get; set; }
    [MaxLength(10000)]
    public string? Notes { get; set; }
    public List<CustomerContactRequest>? Contacts { get; set; }
    public List<ContactPersonRequest>? Persons { get; set; }
}

public class ContactPersonRequest
{
    // Null = create new; set = update the existing person with this id
    public Guid? Id { get; set; }
    [Required, MaxLength(200)]
    public string Name { get; set; } = string.Empty;
    [MaxLength(100)]
    public string? Role { get; set; }
    [MaxLength(50)]
    public string? Phone { get; set; }
    [MaxLength(200)]
    public string? Email { get; set; }
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
