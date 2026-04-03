namespace WorkHub.Models;

public class AddressSuggestionResponse
{
    public string PlaceId { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public string MainText { get; set; } = string.Empty;
    public string SecondaryText { get; set; } = string.Empty;
}

public class AddressDetailsResponse
{
    public string FormattedAddress { get; set; } = string.Empty;
    public string Street { get; set; } = string.Empty;
    public string City { get; set; } = string.Empty;
    public string State { get; set; } = string.Empty;
    public string Zip { get; set; } = string.Empty;
}
