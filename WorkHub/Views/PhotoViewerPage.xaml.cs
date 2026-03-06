using WorkHub.ViewModels;

namespace WorkHub.Views;

public partial class PhotoViewerPage : ContentPage
{
    public PhotoViewerPage(PhotoViewerViewModel viewModel)
    {
        InitializeComponent();
        BindingContext = viewModel;
    }
}
