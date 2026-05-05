using WorkHub.ViewModels;

namespace WorkHub.Views;

public partial class CalendarDaySummaryPage : ContentPage
{
    public CalendarDaySummaryPage(CalendarDaySummaryViewModel viewModel)
    {
        InitializeComponent();
        BindingContext = viewModel;
        BackBtn.IsVisible = MainLayout.Current?.IsWideLayout != true;
    }
}
