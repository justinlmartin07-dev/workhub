using System.ComponentModel.DataAnnotations;

namespace WorkHub.Api.DTOs.Requests;

public class CreateCustomerRequest
{
    [Required, MaxLength(200)]
    public string Name { get; set; } = string.Empty;
    [MaxLength(50)]
    public string? Phone { get; set; }
    [MaxLength(200), EmailAddress]
    public string? Email { get; set; }
    public string? Address { get; set; }
    public string? Notes { get; set; }
}

public class UpdateCustomerRequest
{
    [MaxLength(200)]
    public string? Name { get; set; }
    [MaxLength(50)]
    public string? Phone { get; set; }
    [MaxLength(200)]
    public string? Email { get; set; }
    public string? Address { get; set; }
    public string? Notes { get; set; }
}
