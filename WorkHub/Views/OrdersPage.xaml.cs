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
        if (Handler != null && _viewModel.Orders.Count == 0)
        {
            _viewModel.LoadOrdersCommand.Execute(null);
        }
    }
}
