using WorkHub.ViewModels;

namespace WorkHub.Views;

public partial class CalendarPage : ContentView
{
    private readonly CalendarViewModel _viewModel;

    public CalendarPage(CalendarViewModel viewModel)
    {
        InitializeComponent();
        _viewModel = viewModel;
        BindingContext = viewModel;
    }

    protected override void OnHandlerChanged()
    {
        base.OnHandlerChanged();
        if (Handler != null && _viewModel.Events.Count == 0)
        {
            _viewModel.LoadEventsCommand.Execute(null);
        }
    }
}
