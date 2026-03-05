namespace WorkHub.Api.DTOs.Responses;

public class LocationPhotoGroupResponse
{
    public Guid? CustomerId { get; set; }
    public string? CustomerName { get; set; }
    public Guid? JobId { get; set; }
    public string? JobTitle { get; set; }
    public List<PhotoResponse> Photos { get; set; } = [];
}
