using WorkHub.ViewModels;

namespace WorkHub.Views;

public partial class InventoryItemDetailPage : ContentPage
{
    public InventoryItemDetailPage(InventoryItemDetailViewModel viewModel)
    {
        InitializeComponent();
        BindingContext = viewModel;
    }
}
