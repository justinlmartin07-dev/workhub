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
        _viewModel.ScrollToRowRequested += OnScrollToRowRequested;
    }

    private void OnScrollToRowRequested(object row)
    {
        // Deferred so the scroll runs after the merge's row moves have been applied
        // to the native list. MakeVisible + no animation is the only ScrollTo mode
        // WinUI handles reliably — Center/animated lands at approximated offsets.
        // Two passes: with virtualization the first scroll targets an estimated
        // position (often one row short); the second, against the now-realized
        // container, lands exactly. No-op if the first pass was already right.
        Dispatcher.DispatchDelayed(TimeSpan.FromMilliseconds(150), () =>
            InventoryCollectionView.ScrollTo(row, position: ScrollToPosition.MakeVisible, animate: false));
        Dispatcher.DispatchDelayed(TimeSpan.FromMilliseconds(500), () =>
            InventoryCollectionView.ScrollTo(row, position: ScrollToPosition.MakeVisible, animate: false));
    }

    protected override void OnHandlerChanged()
    {
        base.OnHandlerChanged();
        // Fires on every reattach (tab switch back) — the VM shows the loading
        // state on first load and does a silent incremental merge after that.
        if (Handler != null)
        {
            _viewModel.LoadItemsCommand.Execute(null);
        }
    }
}
