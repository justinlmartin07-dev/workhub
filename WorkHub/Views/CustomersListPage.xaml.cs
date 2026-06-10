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
        // Fires on every reattach (tab switch back) — the VM shows the loading
        // state on first load and does a silent incremental merge after that.
        if (Handler != null)
        {
            _viewModel.LoadCustomersCommand.Execute(null);
        }
    }
}
