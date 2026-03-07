using WorkHub.ViewModels;

namespace WorkHub.Views;

public partial class CalendarDaySummaryPage : ContentPage
{
    public CalendarDaySummaryPage(CalendarDaySummaryViewModel viewModel)
    {
        InitializeComponent();
        BindingContext = viewModel;
        BackButton.IsVisible = MainLayout.Current?.IsWideLayout != true;
    }
}
