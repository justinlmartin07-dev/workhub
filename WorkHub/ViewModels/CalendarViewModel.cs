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

    [ObservableProperty]
    private ObservableCollection<CalendarWeek> _weeks = new();

    [ObservableProperty]
    private string _monthYearLabel = string.Empty;

    public CalendarViewModel(ApiService apiService)
    {
        _apiService = apiService;
    }

    partial void OnSelectedDateChanged(DateTime value)
    {
        FilterDayEvents();
        HighlightSelectedDay();
    }

    [RelayCommand]
    public async Task LoadEventsAsync()
    {
        await LoadAsync(async () =>
        {
            var from = new DateTime(SelectedDate.Year, SelectedDate.Month, 1, 0, 0, 0, DateTimeKind.Utc);
            var to = from.AddMonths(1).AddSeconds(-1);
            var events = await _apiService.GetEventsAsync(from, to);
            Events = new ObservableCollection<CalendarEventResponse>(events);
            BuildGrid();
            FilterDayEvents();
            SetContent();
        });
    }

    private void BuildGrid()
    {
        var firstOfMonth = new DateTime(SelectedDate.Year, SelectedDate.Month, 1);
        var daysInMonth = DateTime.DaysInMonth(SelectedDate.Year, SelectedDate.Month);
        MonthYearLabel = firstOfMonth.ToString("MMMM yyyy");

        // Sunday = 0
        int startDow = (int)firstOfMonth.DayOfWeek;

        var weeks = new List<CalendarWeek>();
        var currentWeek = new CalendarDay[7];

        // Fill leading blanks
        for (int i = 0; i < startDow; i++)
            currentWeek[i] = new CalendarDay();

        for (int day = 1; day <= daysInMonth; day++)
        {
            var date = new DateTime(SelectedDate.Year, SelectedDate.Month, day);
            int dow = (int)date.DayOfWeek;

            var dayEvents = Events
                .Where(e => e.StartTime.ToLocalTime().Date == date.Date)
                .OrderBy(e => e.StartTime)
                .ToList();

            currentWeek[dow] = new CalendarDay
            {
                Date = date,
                DayNumber = day.ToString(),
                IsCurrentMonth = true,
                IsToday = date.Date == DateTime.Today,
                IsSelected = date.Date == SelectedDate.Date,
                Events = new ObservableCollection<CalendarEventResponse>(dayEvents),
            };

            if (dow == 6 || day == daysInMonth)
            {
                // Fill trailing blanks
                for (int i = dow + 1; i < 7; i++)
                    currentWeek[i] = new CalendarDay();

                weeks.Add(new CalendarWeek
                {
                    Sun = currentWeek[0] ?? new(),
                    Mon = currentWeek[1] ?? new(),
                    Tue = currentWeek[2] ?? new(),
                    Wed = currentWeek[3] ?? new(),
                    Thu = currentWeek[4] ?? new(),
                    Fri = currentWeek[5] ?? new(),
                    Sat = currentWeek[6] ?? new(),
                });
                currentWeek = new CalendarDay[7];
            }
        }

        Weeks = new ObservableCollection<CalendarWeek>(weeks);
    }

    private void FilterDayEvents()
    {
        var dayEvents = Events.Where(e => e.StartTime.ToLocalTime().Date == SelectedDate.Date)
                              .OrderBy(e => e.StartTime)
                              .ToList();
        DayEvents = new ObservableCollection<CalendarEventResponse>(dayEvents);
    }

    private void HighlightSelectedDay()
    {
        foreach (var week in Weeks)
        {
            foreach (var day in week.AllDays)
            {
                day.IsSelected = day.IsCurrentMonth && day.Date.Date == SelectedDate.Date;
            }
        }
    }

    [RelayCommand]
    private void SelectDay(CalendarDay? day)
    {
        if (day == null || !day.IsCurrentMonth) return;
        SelectedDate = day.Date;

        var dayEvents = Events
            .Where(e => e.StartTime.ToLocalTime().Date == day.Date.Date)
            .OrderBy(e => e.StartTime)
            .ToList();

        WeakReferenceMessenger.Default.Send(new ShowDetailMessage(new DetailRequest
        {
            Route = "daySummary",
            Properties = new()
            {
                ["SelectedDate"] = day.Date,
                ["Events"] = new ObservableCollection<CalendarEventResponse>(dayEvents)
            },
            QueryParams = new() { ["date"] = day.Date.ToString("yyyy-MM-dd") }
        }));
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

public partial class CalendarDay : ObservableObject
{
    public DateTime Date { get; set; }
    public string DayNumber { get; set; } = string.Empty;
    public bool IsCurrentMonth { get; set; }
    public bool IsToday { get; set; }

    [ObservableProperty]
    private bool _isSelected;

    public ObservableCollection<CalendarEventResponse> Events { get; set; } = new();
}

public class CalendarWeek
{
    public CalendarDay Sun { get; set; } = new();
    public CalendarDay Mon { get; set; } = new();
    public CalendarDay Tue { get; set; } = new();
    public CalendarDay Wed { get; set; } = new();
    public CalendarDay Thu { get; set; } = new();
    public CalendarDay Fri { get; set; } = new();
    public CalendarDay Sat { get; set; } = new();

    public CalendarDay[] AllDays => [Sun, Mon, Tue, Wed, Thu, Fri, Sat];
}
