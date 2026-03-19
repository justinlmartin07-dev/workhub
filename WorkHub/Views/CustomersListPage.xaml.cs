using WorkHub.Models;
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

        _viewModel.ScrollToRequested += OnScrollToRequested;
    }

    private async void OnScrollToRequested(CustomerResponse customer)
    {
        // Wait for CollectionView to render the new items
        await Task.Delay(100);
        MainThread.BeginInvokeOnMainThread(() =>
        {
            CustomersCollectionView.SelectedItem = customer;
            CustomersCollectionView.ScrollTo(customer, position: ScrollToPosition.Center, animate: true);
        });
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
