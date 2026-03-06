using CommunityToolkit.Mvvm.Messaging.Messages;

namespace WorkHub.Messages;

public class ShowDetailMessage : ValueChangedMessage<DetailRequest>
{
    public ShowDetailMessage(DetailRequest value) : base(value) { }
}

public class DetailRequest
{
    public string Route { get; set; } = string.Empty;
    // Key = VM property name, Value = value
    public Dictionary<string, string> Properties { get; set; } = new();
    // Key = query param name, Value = value (for Shell navigation)
    public Dictionary<string, string> QueryParams { get; set; } = new();
}
