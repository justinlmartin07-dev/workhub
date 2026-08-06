using WorkHub.ViewModels;

namespace WorkHub.Views;

public partial class InventoryItemDetailPage : ContentPage
{
    public bool IsNarrowLayout { get; }

    public InventoryItemDetailPage(InventoryItemDetailViewModel viewModel)
    {
        IsNarrowLayout = !(MainLayout.Current?.IsWideLayout ?? false);
        InitializeComponent();
        BindingContext = viewModel;
    }

    // Delay so a tap on a suggestion (which unfocuses the entry first) still
    // lands before the dropdown collapses.
    private async void OnMarkupEntryUnfocused(object sender, FocusEventArgs e)
    {
        await Task.Delay(250);
        (BindingContext as InventoryItemDetailViewModel)?.HideMarkupSuggestionsDeferred();
    }

    // Taps on a suggestion bubble up here too — delay so the selection tap
    // is processed before the flyout collapses.
    private async void OnPageTapped(object sender, TappedEventArgs e)
    {
        await Task.Delay(250);
        (BindingContext as InventoryItemDetailViewModel)?.HideMarkupSuggestionsDeferred();
    }

    private void OnOtherFieldFocused(object sender, FocusEventArgs e)
        => (BindingContext as InventoryItemDetailViewModel)?.HideMarkupSuggestionsDeferred();
}
