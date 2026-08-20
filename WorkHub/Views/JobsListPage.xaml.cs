using WorkHub.Models;
using WorkHub.ViewModels;

namespace WorkHub.Views;

public partial class JobsListPage : ContentView
{
    private readonly JobsListViewModel _viewModel;

    public JobsListPage(JobsListViewModel viewModel)
    {
        InitializeComponent();
        _viewModel = viewModel;
        BindingContext = viewModel;
        Controls.PullToRefresh.Enable(ListStateView);
    }

    private async void OnSortTapped(object? sender, TappedEventArgs e)
    {
        var selection = await SortOptionsPopup.ShowAsync(SortButton, _viewModel.SortKey, _viewModel.SortAscending);
        if (selection != null)
            _viewModel.SetSort(selection.Key, selection.Ascending);
    }

    private async void OnScrollToRequested(JobListItemResponse job)
    {
        await Task.Delay(100);
        MainThread.BeginInvokeOnMainThread(() =>
        {
            JobsCollectionView.SelectedItem = job;
            JobsCollectionView.ScrollTo(job, position: ScrollToPosition.Center, animate: true);
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
            _viewModel.LoadJobsCommand.Execute(null);
        }
    }
}
