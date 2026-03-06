using WorkHub.ViewModels;

namespace WorkHub.Views;

public partial class LocationPhotosPage : ContentPage
{
    public LocationPhotosPage(LocationPhotosViewModel viewModel)
    {
        InitializeComponent();
        BindingContext = viewModel;
    }
}
