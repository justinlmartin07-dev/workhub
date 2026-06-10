using WorkHub.ViewModels;

namespace WorkHub.Views;

public partial class OrdersPage : ContentView
{
    private readonly OrdersViewModel _viewModel;

    public OrdersPage(OrdersViewModel viewModel)
    {
        InitializeComponent();
        _viewModel = viewModel;
        BindingContext = viewModel;
    }

    protected override void OnHandlerChanged()
    {
        base.OnHandlerChanged();
        // Fires on every reattach (tab switch back) — the VM shows the loading
        // state on first load and does a silent incremental merge after that.
        if (Handler != null)
        {
            _viewModel.LoadOrdersCommand.Execute(null);
        }
    }
}
