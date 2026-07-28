using CommunityToolkit.Maui.Views;

namespace WorkHub.Views;

/// <summary>Key is "priority", "status", or null for the default (API) order.</summary>
public record JobSortSelection(string? Key, bool Ascending);

public partial class SortOptionsPopup : Popup
{
    public SortOptionsPopup(string? key, bool ascending)
    {
        InitializeComponent();

        var active = (key, ascending) switch
        {
            ("priority", false) => PriorityHighLabel,
            ("priority", true) => PriorityLowLabel,
            ("status", true) => StatusActiveLabel,
            ("status", false) => StatusDoneLabel,
            _ => DefaultLabel,
        };
        if (Application.Current?.Resources is { } res
            && res.TryGetValue("Primary", out var light)
            && res.TryGetValue("PrimaryDark", out var dark))
        {
            active.SetAppThemeColor(Label.TextColorProperty, (Color)light, (Color)dark);
        }
    }

    private void OnDefaultTapped(object? sender, TappedEventArgs e) => Close(new JobSortSelection(null, false));
    private void OnPriorityHighTapped(object? sender, TappedEventArgs e) => Close(new JobSortSelection("priority", false));
    private void OnPriorityLowTapped(object? sender, TappedEventArgs e) => Close(new JobSortSelection("priority", true));
    private void OnStatusActiveTapped(object? sender, TappedEventArgs e) => Close(new JobSortSelection("status", true));
    private void OnStatusDoneTapped(object? sender, TappedEventArgs e) => Close(new JobSortSelection("status", false));

    // Returns null when dismissed without picking an option.
    public static async Task<JobSortSelection?> ShowAsync(View anchor, string? key, bool ascending)
    {
        var page = Application.Current?.Windows.FirstOrDefault()?.Page;
        if (page == null) return null;

        var popup = new SortOptionsPopup(key, ascending) { Anchor = anchor };
        return await page.ShowPopupAsync(popup) as JobSortSelection;
    }
}
