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
    private readonly AuthService _authService;

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

    private readonly ListCacheService _listCache;
    private DateTime? _loadedMonth;

    public string UserName => _authService.CurrentUser?.Name ?? "";
    public string UserInitials
    {
        get
        {
            var parts = UserName.Split(' ', StringSplitOptions.RemoveEmptyEntries);
            return parts.Length switch { 0 => "?", 1 => parts[0][..1].ToUpper(), _ => $"{parts[0][0]}{parts[^1][0]}".ToUpper() };
        }
    }
    [ObservableProperty]
    private string? _userPhotoUrl;

    [RelayCommand]
    private async Task GoToProfileAsync() => await Shell.Current.GoToAsync("profile");

    public CalendarViewModel(ApiService apiService, ListCacheService listCache, AuthService authService)
    {
        _apiService = apiService;
        _listCache = listCache;
        _authService = authService;
        _userPhotoUrl = authService.CurrentUser?.ProfilePhotoUrl;

        WeakReferenceMessenger.Default.Register<DataChangedMessage>(this, (r, m) =>
        {
            if (m.Value == "event")
                MainThread.BeginInvokeOnMainThread(() => LoadEventsCommand.Execute(null));
            else if (m.Value == "user_photo")
                MainThread.BeginInvokeOnMainThread(() => UserPhotoUrl = _authService.CurrentUser?.ProfilePhotoUrl);
        });
    }

    partial void OnSelectedDateChanged(DateTime value)
    {
        FilterDayEvents();
        HighlightSelectedDay();
    }

    protected override Task OnRefreshRequestedAsync() => LoadEventsAsync();

    [RelayCommand]
    public async Task LoadEventsAsync()
    {
        var month = new DateTime(SelectedDate.Year, SelectedDate.Month, 1);
        var sameMonth = _loadedMonth == month;
        var cacheKey = $"events-{month:yyyy-MM}";

        // Entering a month we haven't loaded this run: render the cached copy
        // instantly and let the network pass below correct it silently.
        if (!sameMonth && !IsBusy)
        {
            var cached = await _listCache.LoadAsync<CalendarEventResponse>(cacheKey);
            if (cached != null && _loadedMonth != month)
            {
                Events = new ObservableCollection<CalendarEventResponse>(cached);
                BuildGrid();
                FilterDayEvents();
                SetContent();
                _loadedMonth = month;
                sameMonth = true;
            }
        }

        await LoadAsync(async () =>
        {
            var from = new DateTime(SelectedDate.Year, SelectedDate.Month, 1, 0, 0, 0, DateTimeKind.Utc);
            var to = from.AddMonths(1).AddSeconds(-1);
            var events = await _apiService.GetEventsAsync(from, to);
            _loadedMonth = month;
            _ = _listCache.SaveAsync(cacheKey, events);

            // Refreshing the same month with identical events — skip the expensive
            // grid rebuild entirely.
            if (sameMonth && EventsUnchanged(events))
            {
                SetContent();
                return;
            }

            Events = new ObservableCollection<CalendarEventResponse>(events);
            BuildGrid();
            FilterDayEvents();
            SetContent();
        }, showLoading: !sameMonth);
    }

    private bool EventsUnchanged(List<CalendarEventResponse> fresh)
    {
        if (fresh.Count != Events.Count) return false;
        var current = Events.OrderBy(e => e.Id).ToList();
        var incoming = fresh.OrderBy(e => e.Id).ToList();
        for (int i = 0; i < current.Count; i++)
        {
            var a = current[i];
            var b = incoming[i];
            if (a.Id != b.Id
                || a.Title != b.Title
                || a.Description != b.Description
                || a.StartTime != b.StartTime
                || a.EndTime != b.EndTime
                || a.CustomerId != b.CustomerId
                || a.JobId != b.JobId
                || a.Assignments.Count != b.Assignments.Count)
                return false;
        }
        return true;
    }

    private void BuildGrid()
    {
        var firstOfMonth = new DateTime(SelectedDate.Year, SelectedDate.Month, 1);
        var daysInMonth = DateTime.DaysInMonth(SelectedDate.Year, SelectedDate.Month);
        var lastOfMonth = new DateTime(SelectedDate.Year, SelectedDate.Month, daysInMonth);
        MonthYearLabel = firstOfMonth.ToString("MMMM yyyy");

        int startDow = (int)firstOfMonth.DayOfWeek;
        var firstSunday = firstOfMonth.AddDays(-startDow);

        var weeks = new List<CalendarWeek>();
        var weekStart = firstSunday;

        while (weekStart <= lastOfMonth)
        {
            var weekEnd = weekStart.AddDays(6);
            var week = new CalendarWeek { WeekStart = weekStart, WeekEnd = weekEnd };

            for (int i = 0; i < 7; i++)
            {
                var date = weekStart.AddDays(i);
                week.SetDay(i, new CalendarDay
                {
                    Date = date,
                    DayNumber = date.Day.ToString(),
                    IsCurrentMonth = date.Month == SelectedDate.Month,
                    IsToday = date.Date == DateTime.Today,
                    IsSelected = date.Date == SelectedDate.Date,
                    Events = new ObservableCollection<CalendarEventResponse>(),
                });
            }

            week.EventBars = ComputeWeekBars(weekStart, weekEnd);
            weeks.Add(week);
            weekStart = weekStart.AddDays(7);
        }

        Weeks = new ObservableCollection<CalendarWeek>(weeks);
    }

    private List<WeekEventBar> ComputeWeekBars(DateTime weekStart, DateTime weekEnd)
    {
        var weekEvents = Events.Where(e =>
        {
            var eStart = e.StartTime.ToLocalTime().Date;
            var eEnd = (e.EndTime?.ToLocalTime() ?? e.StartTime.ToLocalTime()).Date;
            return eStart <= weekEnd && eEnd >= weekStart;
        })
        .OrderBy(e => e.StartTime.ToLocalTime().Date)
        .ThenByDescending(e => ((e.EndTime ?? e.StartTime) - e.StartTime).TotalDays)
        .ToList();

        var laneEnds = new List<int>();
        var bars = new List<WeekEventBar>();

        foreach (var evt in weekEvents)
        {
            var eStart = evt.StartTime.ToLocalTime().Date;
            var eEnd = (evt.EndTime?.ToLocalTime() ?? evt.StartTime.ToLocalTime()).Date;

            int startCol = eStart < weekStart ? 0 : (int)(eStart - weekStart).TotalDays;
            int endCol = eEnd > weekEnd ? 6 : (int)(eEnd - weekStart).TotalDays;
            int span = endCol - startCol + 1;

            int lane = 0;
            while (lane < laneEnds.Count && laneEnds[lane] >= startCol)
                lane++;
            if (lane >= laneEnds.Count)
                laneEnds.Add(endCol);
            else
                laneEnds[lane] = endCol;

            bars.Add(new WeekEventBar
            {
                Event = evt,
                StartColumn = startCol,
                ColumnSpan = span,
                Lane = lane,
                ContinuesLeft = eStart < weekStart,
                ContinuesRight = eEnd > weekEnd,
            });
        }

        return bars;
    }

    private void FilterDayEvents()
    {
        var dayEvents = Events.Where(e => e.StartTime.ToLocalTime().Date == SelectedDate.Date)
                              .OrderBy(e => e.StartTime)
                              .ToList();
        DayEvents = new ObservableCollection<CalendarEventResponse>(dayEvents);
    }

    private void HighlightDay(CalendarDay target)
    {
        foreach (var week in Weeks)
        {
            foreach (var day in week.AllDays)
            {
                day.IsSelected = day == target;
            }
        }
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

        // Highlight immediately before any async work
        HighlightDay(day);

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
    private void AddEvent()
    {
        WeakReferenceMessenger.Default.Send(new ShowDetailMessage(new DetailRequest
        {
            Route = "eventDetail",
            Properties = new() { ["InitialDate"] = SelectedDate.ToString("yyyy-MM-dd") },
            QueryParams = new() { ["date"] = SelectedDate.ToString("yyyy-MM-dd") }
        }));
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

    public DateTime WeekStart { get; set; }
    public DateTime WeekEnd { get; set; }
    public List<WeekEventBar> EventBars { get; set; } = new();

    public CalendarDay[] AllDays => [Sun, Mon, Tue, Wed, Thu, Fri, Sat];

    public void SetDay(int index, CalendarDay day)
    {
        switch (index)
        {
            case 0: Sun = day; break;
            case 1: Mon = day; break;
            case 2: Tue = day; break;
            case 3: Wed = day; break;
            case 4: Thu = day; break;
            case 5: Fri = day; break;
            case 6: Sat = day; break;
        }
    }
}

public class WeekEventBar
{
    public CalendarEventResponse Event { get; set; } = null!;
    public int StartColumn { get; set; }
    public int ColumnSpan { get; set; }
    public int Lane { get; set; }
    public bool ContinuesLeft { get; set; }
    public bool ContinuesRight { get; set; }
}
