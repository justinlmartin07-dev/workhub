namespace WorkHub.Models;

public class PhotoResponse
{
    public Guid Id { get; set; }
    public string Url { get; set; } = string.Empty;
    public string FileName { get; set; } = string.Empty;
    public DateTime UploadedAt { get; set; }
}

public class LocationPhotoGroupResponse
{
    public Guid? CustomerId { get; set; }
    public string? CustomerName { get; set; }
    public Guid? JobId { get; set; }
    public string? JobTitle { get; set; }
    public List<PhotoResponse> Photos { get; set; } = new();
}
