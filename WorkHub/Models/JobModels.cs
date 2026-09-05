using CommunityToolkit.Mvvm.ComponentModel;

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

    public string Display => string.IsNullOrWhiteSpace(Role) ? Name : $"{Name} — {Role}";
    public bool HasPhone => !string.IsNullOrWhiteSpace(Phone);
    public bool HasEmail => !string.IsNullOrWhiteSpace(Email);
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

    public bool IsEdited => UpdatedAt.HasValue;
}

public partial class JobItemResponse : ObservableObject
{
    public Guid Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public string? Description { get; set; }
    public string? PartNumber { get; set; }

    private decimal _quantity;
    // Fractional quantities are allowed (e.g. 1.56). The setter strips trailing
    // zeros (numeric(10,2) arrives as 5.00) so the bound stepper Entry shows "5".
    public decimal Quantity
    {
        get => _quantity;
        set
        {
            if (SetProperty(ref _quantity, value / 1.000000000000000000000000000000m))
                OnPropertyChanged(nameof(PriceDisplay));
        }
    }
    public string ListType { get; set; } = string.Empty;
    public string Source { get; set; } = string.Empty;
    public Guid? InventoryItemId { get; set; }
    public DateTime? OrderedAt { get; set; }
    public decimal? Cost { get; set; }
    public decimal? Price { get; set; }

    public bool IsOrdered => OrderedAt != null;

    // Compact extended "cost / price" (unit × quantity) for the job item rows;
    // a missing half shows as a dash.
    public string PriceDisplay =>
        Cost == null && Price == null
            ? string.Empty
            : $"{(Cost * Quantity)?.ToString("C") ?? "—"} / {(Price * Quantity)?.ToString("C") ?? "—"}";
}

public class CreateJobRequest
{
    public Guid CustomerId { get; set; }
    public string Title { get; set; } = string.Empty;
    public string? Status { get; set; }
    public string? Priority { get; set; }
    public string? ScopeNotes { get; set; }
    public string? Address { get; set; }
    public Guid? MainContactId { get; set; }
}

public class UpdateJobRequest
{
    public string? Title { get; set; }
    public string? Status { get; set; }
    public string? Priority { get; set; }
    public string? ScopeNotes { get; set; }
    public string? Address { get; set; }
    // Null = don't change; Guid.Empty = clear
    public Guid? MainContactId { get; set; }
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
    public decimal Quantity { get; set; } = 1;
    public string ListType { get; set; } = string.Empty;
}

public class UpdateJobInventoryRequest
{
    public decimal? Quantity { get; set; }
    public string? ListType { get; set; }
    public bool? Ordered { get; set; }
}

public class CreateJobAdhocItemRequest
{
    public string Name { get; set; } = string.Empty;
    public string? Description { get; set; }
    public decimal Quantity { get; set; } = 1;
    public string ListType { get; set; } = string.Empty;
}

public class UpdateJobAdhocItemRequest
{
    public string? Name { get; set; }
    public string? Description { get; set; }
    public decimal? Quantity { get; set; }
    public string? ListType { get; set; }
    public bool? Ordered { get; set; }
}
