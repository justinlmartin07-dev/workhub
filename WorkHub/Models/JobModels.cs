namespace WorkHub.Models;

public class JobResponse
{
    public Guid Id { get; set; }
    public Guid CustomerId { get; set; }
    public string CustomerName { get; set; } = string.Empty;
    public string Title { get; set; } = string.Empty;
    public string Status { get; set; } = string.Empty;
    public string Priority { get; set; } = string.Empty;
    public string? ScopeNotes { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime UpdatedAt { get; set; }
    public List<PhotoResponse>? Photos { get; set; }
    public List<JobNoteResponse>? Notes { get; set; }
    public List<JobItemResponse>? UsedItems { get; set; }
    public List<JobItemResponse>? ToOrderItems { get; set; }
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
}

public class JobItemResponse
{
    public Guid Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public string? Description { get; set; }
    public string? PartNumber { get; set; }
    public int Quantity { get; set; }
    public string ListType { get; set; } = string.Empty;
    public string Source { get; set; } = string.Empty;
    public Guid? InventoryItemId { get; set; }
}

public class CreateJobRequest
{
    public Guid CustomerId { get; set; }
    public string Title { get; set; } = string.Empty;
    public string? Status { get; set; }
    public string? Priority { get; set; }
    public string? ScopeNotes { get; set; }
}

public class UpdateJobRequest
{
    public string? Title { get; set; }
    public string? Status { get; set; }
    public string? Priority { get; set; }
    public string? ScopeNotes { get; set; }
}

public class CreateJobNoteRequest
{
    public string Content { get; set; } = string.Empty;
}

public class UpdateJobNoteRequest
{
    public string Content { get; set; } = string.Empty;
}

public class CreateJobInventoryRequest
{
    public Guid InventoryItemId { get; set; }
    public int Quantity { get; set; } = 1;
    public string ListType { get; set; } = string.Empty;
}

public class UpdateJobInventoryRequest
{
    public int? Quantity { get; set; }
    public string? ListType { get; set; }
}

public class CreateJobAdhocItemRequest
{
    public string Name { get; set; } = string.Empty;
    public string? Description { get; set; }
    public int Quantity { get; set; } = 1;
    public string ListType { get; set; } = string.Empty;
}

public class UpdateJobAdhocItemRequest
{
    public string? Name { get; set; }
    public string? Description { get; set; }
    public int? Quantity { get; set; }
    public string? ListType { get; set; }
}
