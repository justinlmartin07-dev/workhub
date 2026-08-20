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
        Controls.PullToRefresh.Enable(ListStateView);
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
        // The VM is a singleton — only the attached page instance may listen,
        // or handlers from stale pages would pile up across login cycles.
        _viewModel.ScrollToRequested -= OnScrollToRequested;
        // Fires on every reattach (tab switch back) — the VM shows the loading
        // state on first load and does a silent incremental merge after that.
        if (Handler != null)
        {
            _viewModel.ScrollToRequested += OnScrollToRequested;
            _viewModel.LoadCustomersCommand.Execute(null);
        }
    }
}
