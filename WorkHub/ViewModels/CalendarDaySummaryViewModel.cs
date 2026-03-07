using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using CommunityToolkit.Mvvm.Messaging;
using WorkHub.Messages;
using WorkHub.Models;
using WorkHub.Services;

namespace WorkHub.ViewModels;

[QueryProperty(nameof(DateParam), "date")]
public partial class CalendarDaySummaryViewModel : BaseViewModel
{
    private readonly ApiService _apiService;

    [ObservableProperty]
    private DateTime _selectedDate;

    [ObservableProperty]
    private ObservableCollection<CalendarEventResponse> _events = new();

    [ObservableProperty]
    private string _dateLabel = string.Empty;

    [ObservableProperty]
    private string _dayOfWeekLabel = string.Empty;

    private string? _dateParam;
    public string? DateParam
    {
        get => _dateParam;
        set
        {
            _dateParam = value;
            if (DateTime.TryParse(value, out var date))
            {
                SelectedDate = date;
            }
        }
    }

    public CalendarDaySummaryViewModel(ApiService apiService)
    {
        _apiService = apiService;
    }

    partial void OnSelectedDateChanged(DateTime value)
    {
        DateLabel = value.ToString("MMMM d, yyyy");
        DayOfWeekLabel = value.ToString("dddd");
        LoadEventsCommand.Execute(null);
    }

    [RelayCommand]
    private async Task LoadEventsAsync()
    {
        try
        {
            var from = DateTime.SpecifyKind(SelectedDate.Date, DateTimeKind.Utc);
            var to = from.AddDays(1).AddSeconds(-1);
            var events = await _apiService.GetEventsAsync(from, to);
            var dayEvents = events
                .Where(e => e.StartTime.ToLocalTime().Date == SelectedDate.Date)
                .OrderBy(e => e.StartTime)
                .ToList();
            Events = new ObservableCollection<CalendarEventResponse>(dayEvents);
        }
        catch { }
    }

    [RelayCommand]
    private async Task GoBackAsync()
    {
        await Shell.Current.GoToAsync("..");
    }

    [RelayCommand]
    private void SelectEvent(CalendarEventResponse? evt)
    {
        if (evt == null) return;
        var id = evt.Id.ToString();
        WeakReferenceMessenger.Default.Send(new ShowDetailMessage(new DetailRequest
        {
            Route = "eventDetail",
            Properties = new() { ["EventId"] = id },
            QueryParams = new() { ["id"] = id }
        }));
    }

    [RelayCommand]
    private void AddEvent()
    {
        WeakReferenceMessenger.Default.Send(new ShowDetailMessage(new DetailRequest
        {
            Route = "eventDetail",
            Properties = new() { ["InitialDate"] = SelectedDate.ToString("yyyy-MM-dd") },
            QueryParams = new() { ["date"] = SelectedDate.ToString("yyyy-MM-dd") }
        }));
    }
}
