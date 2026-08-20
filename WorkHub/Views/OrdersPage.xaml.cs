using WorkHub.Models;
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
        Controls.PullToRefresh.Enable(ListStateView);
    }

    private async void OnScrollToRequested(OrderLineResponse item)
    {
        await Task.Delay(100);
        MainThread.BeginInvokeOnMainThread(() =>
        {
            OrdersCollectionView.SelectedItem = item;
            OrdersCollectionView.ScrollTo(item, position: ScrollToPosition.Center, animate: true);
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
            _viewModel.LoadOrdersCommand.Execute(null);
        }
    }
}
