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
}
