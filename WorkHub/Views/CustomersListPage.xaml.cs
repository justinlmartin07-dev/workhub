using WorkHub.ViewModels;

namespace WorkHub.Views;

public partial class CustomersListPage : ContentView
{
    private readonly CustomersListViewModel _viewModel;

    public CustomersListPage(CustomersListViewModel viewModel)
    {
        InitializeComponent();
        _viewModel = viewModel;
        BindingContext = viewModel;
    }

    protected override void OnHandlerChanged()
    {
        base.OnHandlerChanged();
        if (Handler != null && _viewModel.Customers.Count == 0)
        {
            _viewModel.LoadCustomersCommand.Execute(null);
        }
    }
}
