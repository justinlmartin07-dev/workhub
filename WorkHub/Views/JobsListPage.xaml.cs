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

        _viewModel.ScrollToRequested += OnScrollToRequested;
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
        if (Handler != null && _viewModel.Jobs.Count == 0)
        {
            _viewModel.LoadJobsCommand.Execute(null);
        }
    }
}
