using WorkHub.ViewModels;

namespace WorkHub.Views;

public partial class InventoryPage : ContentView
{
    private readonly InventoryViewModel _viewModel;

    public InventoryPage(InventoryViewModel viewModel)
    {
        InitializeComponent();
        _viewModel = viewModel;
        BindingContext = viewModel;
    }

    protected override void OnHandlerChanged()
    {
        base.OnHandlerChanged();
        if (Handler != null && _viewModel.Items.Count == 0)
        {
            _viewModel.LoadItemsCommand.Execute(null);
        }
    }
}
