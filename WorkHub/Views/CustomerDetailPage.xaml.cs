using WorkHub.ViewModels;

namespace WorkHub.Views;

public partial class CustomerDetailPage : ContentPage
{
    public CustomerDetailPage(CustomerDetailViewModel viewModel)
    {
        InitializeComponent();
        BindingContext = viewModel;
    }
}
