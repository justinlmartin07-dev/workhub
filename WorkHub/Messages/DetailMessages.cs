using CommunityToolkit.Mvvm.Messaging.Messages;

namespace WorkHub.Messages;

public class ShowDetailMessage : ValueChangedMessage<DetailRequest>
{
    public ShowDetailMessage(DetailRequest value) : base(value) { }
}

public class SelectListItemMessage : ValueChangedMessage<SelectListItemRequest>
{
    public SelectListItemMessage(SelectListItemRequest value) : base(value) { }
}

public class SelectListItemRequest
{
    public string ItemId { get; set; } = string.Empty;
    public int TabIndex { get; set; }
}

public class DataChangedMessage : ValueChangedMessage<string>
{
    public DataChangedMessage(string entityType) : base(entityType) { }
}

// Sent when a single to-order part is marked ordered/unordered, so the
// dashboard can update just that row without reloading the whole list.
public class OrderOrderedChangedMessage : ValueChangedMessage<OrderOrderedChange>
{
    public OrderOrderedChangedMessage(OrderOrderedChange value) : base(value) { }
}

public class OrderOrderedChange
{
    public Guid ItemId { get; set; }
    public string Source { get; set; } = string.Empty;
    public DateTime? OrderedAt { get; set; }
}

public class DetailRequest
{
    public string Route { get; set; } = string.Empty;
    // Key = VM property name, Value = value (string for simple types, object for complex)
    public Dictionary<string, object> Properties { get; set; } = new();
    // Key = query param name, Value = value (for Shell navigation)
    public Dictionary<string, string> QueryParams { get; set; } = new();
    // Optional: switch to this tab index before showing detail
    public int? SwitchTabIndex { get; set; }
}
