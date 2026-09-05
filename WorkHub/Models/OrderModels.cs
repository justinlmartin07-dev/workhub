using CommunityToolkit.Mvvm.ComponentModel;

namespace WorkHub.Models;

// A single "to order" part across all jobs, shown on the ordering dashboard.
public partial class OrderLineResponse : ObservableObject
{
    public Guid Id { get; set; }
    public string Source { get; set; } = string.Empty; // "library" or "adhoc"
    public string Name { get; set; } = string.Empty;
    public string? Description { get; set; }
    public string? PartNumber { get; set; }
    public string? Sku { get; set; }
    public decimal Quantity { get; set; }
    public Guid JobId { get; set; }
    public string JobTitle { get; set; } = string.Empty;
    public Guid CustomerId { get; set; }
    public string CustomerName { get; set; } = string.Empty;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(IsOrdered))]
    private DateTime? _orderedAt;

    public bool IsOrdered => OrderedAt.HasValue;
}
