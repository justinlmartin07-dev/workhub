using CommunityToolkit.Mvvm.ComponentModel;

namespace WorkHub.Models;

public class InventoryItemResponse
{
    public Guid Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public string? Description { get; set; }
    public string? PartNumber { get; set; }
    public string? Category { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime UpdatedAt { get; set; }
}

public class CreateInventoryItemRequest
{
    public string Name { get; set; } = string.Empty;
    public string? Description { get; set; }
    public string? PartNumber { get; set; }
    public string? Category { get; set; }
}

public class UpdateInventoryItemRequest
{
    public string? Name { get; set; }
    public string? Description { get; set; }
    public string? PartNumber { get; set; }
    // null = unchanged, empty string = clear category
    public string? Category { get; set; }
}

/// <summary>
/// Category header row in the flat inventory list. The list is deliberately NOT a
/// grouped CollectionView — WinUI crashes ("Value does not fall within the expected
/// range") when groups are inserted or mutated in place, e.g. on clearing a search.
/// Headers and items live in one flat collection instead.
/// </summary>
public partial class InventoryGroupHeader : ObservableObject
{
    public string Category { get; }

    [ObservableProperty]
    private int _itemCount;

    [ObservableProperty]
    private bool _isExpanded;

    public InventoryGroupHeader(string category, int itemCount, bool isExpanded)
    {
        Category = category;
        _itemCount = itemCount;
        _isExpanded = isExpanded;
    }
}
