namespace WorkHub.Api.DTOs.Responses;

public class CustomerResponse
{
    public Guid Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public string? Address { get; set; }
    public string? Notes { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime UpdatedAt { get; set; }
    public List<CustomerContactResponse>? Contacts { get; set; }
    public List<PhotoResponse>? Photos { get; set; }
    public List<JobBriefResponse>? Jobs { get; set; }
}

public class CustomerContactResponse
{
    public Guid Id { get; set; }
    public string Type { get; set; } = string.Empty;
    public string Label { get; set; } = string.Empty;
    public string Value { get; set; } = string.Empty;
    public bool IsPrimary { get; set; }
}

public class JobBriefResponse
{
    public Guid Id { get; set; }
    public string Title { get; set; } = string.Empty;
    public string Status { get; set; } = string.Empty;
    public string Priority { get; set; } = string.Empty;
}
