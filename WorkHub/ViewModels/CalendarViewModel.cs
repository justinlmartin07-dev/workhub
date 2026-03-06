using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using CommunityToolkit.Mvvm.Messaging;
using WorkHub.Messages;
using WorkHub.Models;
using WorkHub.Services;

namespace WorkHub.ViewModels;

public partial class CalendarViewModel : BaseViewModel
{
    private readonly ApiService _apiService;

    [ObservableProperty]
    private ObservableCollection<CalendarEventResponse> _events = new();

    [ObservableProperty]
    private DateTime _selectedDate = DateTime.Today;

    [ObservableProperty]
    private ObservableCollection<CalendarEventResponse> _dayEvents = new();

    public CalendarViewModel(ApiService apiService)
    {
        _apiService = apiService;
    }

    partial void OnSelectedDateChanged(DateTime value)
    {
        FilterDayEvents();
    }

    [RelayCommand]
    public async Task LoadEventsAsync()
    {
        await LoadAsync(async () =>
        {
            var from = new DateTime(SelectedDate.Year, SelectedDate.Month, 1);
            var to = from.AddMonths(1).AddDays(-1);
            var events = await _apiService.GetEventsAsync(from, to);
            Events = new ObservableCollection<CalendarEventResponse>(events);
            FilterDayEvents();
            if (Events.Count == 0) SetEmpty();
            else SetContent();
        });
    }

    private void FilterDayEvents()
    {
        var dayEvents = Events.Where(e => e.StartTime.Date == SelectedDate.Date)
                              .OrderBy(e => e.StartTime)
                              .ToList();
        DayEvents = new ObservableCollection<CalendarEventResponse>(dayEvents);
    }

    [RelayCommand]
    private void SelectEvent(CalendarEventResponse evt)
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
    private async Task AddEventAsync()
    {
        await Shell.Current.GoToAsync($"eventDetail?date={SelectedDate:yyyy-MM-dd}");
    }

    [RelayCommand]
    private void PreviousMonth()
    {
        SelectedDate = SelectedDate.AddMonths(-1);
        LoadEventsCommand.Execute(null);
    }

    [RelayCommand]
    private void NextMonth()
    {
        SelectedDate = SelectedDate.AddMonths(1);
        LoadEventsCommand.Execute(null);
    }
}
