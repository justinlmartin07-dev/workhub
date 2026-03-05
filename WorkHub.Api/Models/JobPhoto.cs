namespace WorkHub.Api.Models;

public class JobPhoto
{
    public Guid Id { get; set; }
    public Guid JobId { get; set; }
    public Job Job { get; set; } = null!;
    public string R2ObjectKey { get; set; } = string.Empty;
    public string FileName { get; set; } = string.Empty;
    public string? AddressTag { get; set; }
    public DateTime UploadedAt { get; set; }
}
