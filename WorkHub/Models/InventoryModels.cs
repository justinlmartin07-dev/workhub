using CommunityToolkit.Mvvm.ComponentModel;

namespace WorkHub.Models;

// Displayed fields are observable so a refresh can update an existing row
// in place. Replacing the instance in the bound collection makes the WinUI
// list re-render the row (flicker) and drop its scroll position.
public partial class InventoryItemResponse : ObservableObject
{
    public Guid Id { get; set; }

    [ObservableProperty]
    private string _name = string.Empty;

    [ObservableProperty]
    private string? _description;

    [ObservableProperty]
    private string? _partNumber;

    [ObservableProperty]
    private string? _sku;

    [ObservableProperty]
    private string? _category;

    public DateTime CreatedAt { get; set; }
    public DateTime UpdatedAt { get; set; }
}

public class CreateInventoryItemRequest
{
    public string Name { get; set; } = string.Empty;
    public string? Description { get; set; }
    public string? PartNumber { get; set; }
    public string? Sku { get; set; }
    public string? Category { get; set; }
}

// For all optional fields: null = unchanged, empty string = clear.
public class UpdateInventoryItemRequest
{
    public string? Name { get; set; }
    public string? Description { get; set; }
    public string? PartNumber { get; set; }
    public string? Sku { get; set; }
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
