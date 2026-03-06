using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using WorkHub.Models;
using WorkHub.Services;

namespace WorkHub.ViewModels;

[QueryProperty(nameof(EventId), "id")]
[QueryProperty(nameof(InitialDate), "date")]
public partial class EventDetailViewModel : BaseViewModel
{
    private readonly ApiService _apiService;

    [ObservableProperty]
    private string? _eventId;

    [ObservableProperty]
    private string? _initialDate;

    [ObservableProperty]
    private string _title = string.Empty;

    [ObservableProperty]
    private string _description = string.Empty;

    [ObservableProperty]
    private DateTime _startDate = DateTime.Today;

    [ObservableProperty]
    private TimeSpan _startTime = new(9, 0, 0);

    [ObservableProperty]
    private DateTime _endDate = DateTime.Today;

    [ObservableProperty]
    private TimeSpan _endTime = new(10, 0, 0);

    [ObservableProperty]
    private bool _hasEndTime = true;

    [ObservableProperty]
    private ObservableCollection<CustomerResponse> _customers = new();

    [ObservableProperty]
    private CustomerResponse? _selectedCustomer;

    [ObservableProperty]
    private ObservableCollection<UserBriefResponse> _users = new();

    [ObservableProperty]
    private ObservableCollection<UserBriefResponse> _assignedUsers = new();

    [ObservableProperty]
    private bool _isNew = true;

    [ObservableProperty]
    private bool _isEditing = true;

    [ObservableProperty]
    private string _pageTitle = "New Event";

    public EventDetailViewModel(ApiService apiService)
    {
        _apiService = apiService;
    }

    partial void OnEventIdChanged(string? value)
    {
        if (Guid.TryParse(value, out _))
        {
            IsNew = false;
            IsEditing = false;
            PageTitle = "Event Details";
            LoadEventCommand.Execute(null);
        }
    }

    partial void OnInitialDateChanged(string? value)
    {
        if (DateTime.TryParse(value, out var date))
        {
            StartDate = date;
            EndDate = date;
        }
        if (IsNew)
            LoadPickerDataCommand.Execute(null);
    }

    [RelayCommand]
    private async Task LoadPickerDataAsync()
    {
        try
        {
            var customersTask = _apiService.GetCustomersAsync(pageSize: 100);
            var usersTask = _apiService.GetUsersAsync();
            await Task.WhenAll(customersTask, usersTask);
            Customers = new ObservableCollection<CustomerResponse>(customersTask.Result.Items);
            Users = new ObservableCollection<UserBriefResponse>(usersTask.Result);
        }
        catch { }
    }

    [RelayCommand]
    private async Task LoadEventAsync()
    {
        if (!Guid.TryParse(EventId, out var id)) return;
        await LoadAsync(async () =>
        {
            var evt = await _apiService.GetEventAsync(id);
            if (evt == null) return;

            Title = evt.Title;
            Description = evt.Description ?? string.Empty;
            StartDate = evt.StartTime.ToLocalTime().Date;
            StartTime = evt.StartTime.ToLocalTime().TimeOfDay;
            if (evt.EndTime.HasValue)
            {
                HasEndTime = true;
                EndDate = evt.EndTime.Value.ToLocalTime().Date;
                EndTime = evt.EndTime.Value.ToLocalTime().TimeOfDay;
            }
            else
            {
                HasEndTime = false;
            }

            AssignedUsers = new ObservableCollection<UserBriefResponse>(
                evt.Assignments.Select(a => new UserBriefResponse { Id = a.UserId, Name = a.Name }));

            await LoadPickerDataAsync();
            if (evt.CustomerId.HasValue)
                SelectedCustomer = Customers.FirstOrDefault(c => c.Id == evt.CustomerId.Value);
        });
    }

    [RelayCommand]
    private void ToggleEdit()
    {
        IsEditing = !IsEditing;
        PageTitle = IsEditing ? "Edit Event" : "Event Details";
    }

    [RelayCommand]
    private async Task SaveAsync()
    {
        if (string.IsNullOrWhiteSpace(Title))
        {
            ErrorMessage = "Title is required";
            HasError = true;
            return;
        }

        await LoadAsync(async () =>
        {
            var startDateTime = StartDate.Add(StartTime).ToUniversalTime();
            DateTime? endDateTime = HasEndTime ? EndDate.Add(EndTime).ToUniversalTime() : null;

            if (IsNew)
            {
                var request = new CreateEventRequest
                {
                    Title = Title.Trim(),
                    Description = string.IsNullOrWhiteSpace(Description) ? null : Description.Trim(),
                    StartTime = startDateTime,
                    EndTime = endDateTime,
                    CustomerId = SelectedCustomer?.Id,
                    AssignedUserIds = AssignedUsers.Select(u => u.Id).ToList()
                };
                await _apiService.CreateEventAsync(request);
                await Shell.Current.GoToAsync("..");
            }
            else
            {
                var request = new UpdateEventRequest
                {
                    Title = Title.Trim(),
                    Description = string.IsNullOrWhiteSpace(Description) ? null : Description.Trim(),
                    StartTime = startDateTime,
                    EndTime = endDateTime,
                    CustomerId = SelectedCustomer?.Id
                };
                await _apiService.UpdateEventAsync(Guid.Parse(EventId!), request);
                IsEditing = false;
                PageTitle = "Event Details";
            }
        });
    }

    [RelayCommand]
    private async Task DeleteAsync()
    {
        if (IsNew) return;
        bool confirm = await Shell.Current.DisplayAlert("Delete Event", "Delete this event?", "Delete", "Cancel");
        if (!confirm) return;
        try
        {
            await _apiService.DeleteEventAsync(Guid.Parse(EventId!));
            await Shell.Current.GoToAsync("..");
        }
        catch (Exception ex)
        {
            await Shell.Current.DisplayAlert("Error", ex.Message, "OK");
        }
    }

    [RelayCommand]
    private async Task CancelAsync()
    {
        if (!IsNew && IsEditing)
        {
            IsEditing = false;
            PageTitle = "Event Details";
            await LoadEventAsync();
        }
        else
        {
            await Shell.Current.GoToAsync("..");
        }
    }

    [RelayCommand]
    private void AddAssignment(UserBriefResponse user)
    {
        if (!AssignedUsers.Any(u => u.Id == user.Id))
            AssignedUsers.Add(user);
    }

    [RelayCommand]
    private void RemoveAssignment(UserBriefResponse user)
    {
        var existing = AssignedUsers.FirstOrDefault(u => u.Id == user.Id);
        if (existing != null)
            AssignedUsers.Remove(existing);
    }
}
