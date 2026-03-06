using WorkHub.ViewModels;

namespace WorkHub.Views;

public partial class JobDetailPage : ContentPage
{
    public JobDetailPage(JobDetailViewModel viewModel)
    {
        InitializeComponent();
        BindingContext = viewModel;
    }
}
