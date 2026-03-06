using WorkHub.ViewModels;

namespace WorkHub.Views;

public partial class CustomerEditPage : ContentPage
{
    public CustomerEditPage(CustomerEditViewModel viewModel)
    {
        InitializeComponent();
        BindingContext = viewModel;
    }
}
