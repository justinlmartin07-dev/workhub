namespace WorkHub.Api.DTOs.Responses;

public class JobResponse
{
    public Guid Id { get; set; }
    public Guid CustomerId { get; set; }
    public string CustomerName { get; set; } = string.Empty;
    public string Title { get; set; } = string.Empty;
    public string Status { get; set; } = string.Empty;
    public string Priority { get; set; } = string.Empty;
    public string? ScopeNotes { get; set; }
    public string? Address { get; set; }
    public Guid? MainContactId { get; set; }
    public JobContactResponse? MainContact { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime UpdatedAt { get; set; }
    public List<PhotoResponse>? Photos { get; set; }
    public List<JobNoteResponse>? Notes { get; set; }
    public List<JobItemResponse>? UsedItems { get; set; }
    public List<JobItemResponse>? ToOrderItems { get; set; }
}

public class JobContactResponse
{
    public Guid Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public string? Role { get; set; }
    public string? Phone { get; set; }
    public string? Email { get; set; }
}

public class JobListItemResponse
{
    public Guid Id { get; set; }
    public Guid CustomerId { get; set; }
    public string CustomerName { get; set; } = string.Empty;
    public string Title { get; set; } = string.Empty;
    public string Status { get; set; } = string.Empty;
    public string Priority { get; set; } = string.Empty;
    public DateTime CreatedAt { get; set; }
}

public class JobNoteResponse
{
    public Guid Id { get; set; }
    public string Content { get; set; } = string.Empty;
    public Guid CreatedBy { get; set; }
    public string CreatedByName { get; set; } = string.Empty;
    public DateTime CreatedAt { get; set; }
    public DateTime? UpdatedAt { get; set; }
    public string? UpdatedByName { get; set; }
}

public class JobItemResponse
{
    public Guid Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public string? Description { get; set; }
    public string? PartNumber { get; set; }
    public int Quantity { get; set; }
    public string ListType { get; set; } = string.Empty;
    public string Source { get; set; } = string.Empty; // "library" or "adhoc"
    public Guid? InventoryItemId { get; set; }
    public DateTime? OrderedAt { get; set; }
}

// A single "to order" part across all jobs, for the ordering dashboard.
public class OrderLineResponse
{
    public Guid Id { get; set; }
    public string Source { get; set; } = string.Empty; // "library" or "adhoc"
    public string Name { get; set; } = string.Empty;
    public string? Description { get; set; }
    public string? PartNumber { get; set; }
    public int Quantity { get; set; }
    public DateTime? OrderedAt { get; set; }
    public Guid JobId { get; set; }
    public string JobTitle { get; set; } = string.Empty;
    public Guid CustomerId { get; set; }
    public string CustomerName { get; set; } = string.Empty;
}
