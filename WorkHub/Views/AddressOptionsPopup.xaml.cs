using CommunityToolkit.Maui.Views;

namespace WorkHub.Views;

public partial class AddressOptionsPopup : Popup
{
    public AddressOptionsPopup(string address)
    {
        InitializeComponent();
        AddressLabel.Text = address;
    }

    private void OnOpenInEarthTapped(object? sender, TappedEventArgs e) => Close(true);

    // Long-press the address to show; returns true when "Open in Google Earth" was chosen.
    public static async Task<bool> ShowAsync(string address)
    {
        var page = Application.Current?.Windows.FirstOrDefault()?.Page;
        if (page == null) return false;

        var result = await page.ShowPopupAsync(new AddressOptionsPopup(address));
        return result is true;
    }
}
