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
