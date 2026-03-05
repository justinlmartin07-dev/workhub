namespace WorkHub.Api.Models;

public class CustomerPhoto
{
    public Guid Id { get; set; }
    public Guid CustomerId { get; set; }
    public Customer Customer { get; set; } = null!;
    public string R2ObjectKey { get; set; } = string.Empty;
    public string FileName { get; set; } = string.Empty;
    public string? AddressTag { get; set; }
    public DateTime UploadedAt { get; set; }
}
