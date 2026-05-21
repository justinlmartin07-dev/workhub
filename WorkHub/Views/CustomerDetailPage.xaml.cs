using WorkHub.ViewModels;

namespace WorkHub.Views;

public partial class CustomerDetailPage : ContentPage
{
    public bool IsNarrowLayout { get; }

    public CustomerDetailPage(CustomerDetailViewModel viewModel)
    {
        IsNarrowLayout = !(MainLayout.Current?.IsWideLayout ?? false);
        InitializeComponent();
        BindingContext = viewModel;
    }
}
