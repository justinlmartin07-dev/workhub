using WorkHub.ViewModels;

namespace WorkHub.Views;

public partial class EventDetailPage : ContentPage
{
    public EventDetailPage(EventDetailViewModel viewModel)
    {
        InitializeComponent();
        BindingContext = viewModel;
    }
}
