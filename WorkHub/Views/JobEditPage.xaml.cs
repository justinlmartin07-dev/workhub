using WorkHub.ViewModels;

namespace WorkHub.Views;

public partial class JobEditPage : ContentPage
{
    public JobEditPage(JobEditViewModel viewModel)
    {
        InitializeComponent();
        BindingContext = viewModel;
    }
}
