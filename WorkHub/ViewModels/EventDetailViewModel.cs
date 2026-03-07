using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using CommunityToolkit.Mvvm.Messaging;
using WorkHub.Messages;
using WorkHub.Models;
using WorkHub.Services;
using WorkHub.Views;

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
    private bool _isAllDay;

    [ObservableProperty]
    private ObservableCollection<CustomerResponse> _customers = new();

    [ObservableProperty]
    private ObservableCollection<CustomerResponse> _filteredCustomers = new();

    [ObservableProperty]
    private CustomerResponse? _selectedCustomer;

    [ObservableProperty]
    private string _customerSearchText = string.Empty;

    [ObservableProperty]
    private bool _isCustomerPickerOpen;

    [ObservableProperty]
    private ObservableCollection<UserBriefResponse> _users = new();

    [ObservableProperty]
    private ObservableCollection<UserBriefResponse> _assignedUsers = new();

    [ObservableProperty]
    private bool _isNew = true;

    [ObservableProperty]
    private bool _isEditing = true;

    [ObservableProperty]
    private bool _isDirty;

    [ObservableProperty]
    private string _pageTitle = "New Event";

    [ObservableProperty]
    private string _userSearchText = string.Empty;

    [ObservableProperty]
    private ObservableCollection<UserBriefResponse> _filteredUsers = new();

    [ObservableProperty]
    private bool _showUserSuggestions;

    private bool _trackDirty;

    public EventDetailViewModel(ApiService apiService)
    {
        _apiService = apiService;
    }

    partial void OnUserSearchTextChanged(string value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            ShowUserSuggestions = false;
            FilteredUsers.Clear();
            return;
        }

        var search = value.ToLower();
        var assignedIds = AssignedUsers.Select(u => u.Id).ToHashSet();
        var matches = Users
            .Where(u => !assignedIds.Contains(u.Id) && u.Name.ToLower().Contains(search))
            .ToList();

        FilteredUsers = new ObservableCollection<UserBriefResponse>(matches);
        ShowUserSuggestions = matches.Count > 0;
    }

    [RelayCommand]
    private void SelectSuggestion(UserBriefResponse? user)
    {
        if (user == null) return;
        AddAssignment(user);
        UserSearchText = string.Empty;
        ShowUserSuggestions = false;
    }

    partial void OnCustomerSearchTextChanged(string value) => FilterCustomers();

    private void FilterCustomers()
    {
        if (string.IsNullOrWhiteSpace(CustomerSearchText))
        {
            FilteredCustomers = new ObservableCollection<CustomerResponse>(Customers);
        }
        else
        {
            var search = CustomerSearchText.ToLower();
            FilteredCustomers = new ObservableCollection<CustomerResponse>(
                Customers.Where(c =>
                    c.Name.ToLower().Contains(search) ||
                    (c.Contacts?.Any(ct => ct.Value.ToLower().Contains(search)) ?? false) ||
                    (c.Address?.ToLower().Contains(search) ?? false)));
        }
    }

    [RelayCommand]
    private void ToggleCustomerPicker()
    {
        IsCustomerPickerOpen = !IsCustomerPickerOpen;
        if (IsCustomerPickerOpen)
        {
            CustomerSearchText = string.Empty;
            FilterCustomers();
        }
    }

    [RelayCommand]
    private void PickCustomer(CustomerResponse customer)
    {
        SelectedCustomer = customer;
        IsCustomerPickerOpen = false;
    }

    [RelayCommand]
    private void ClearCustomer()
    {
        SelectedCustomer = null;
    }

    partial void OnTitleChanged(string value) => MarkDirty();
    partial void OnDescriptionChanged(string value) => MarkDirty();
    partial void OnStartDateChanged(DateTime value) => MarkDirty();
    partial void OnStartTimeChanged(TimeSpan value) => MarkDirty();
    partial void OnEndDateChanged(DateTime value) => MarkDirty();
    partial void OnEndTimeChanged(TimeSpan value) => MarkDirty();
    partial void OnIsAllDayChanged(bool value) => MarkDirty();
    partial void OnSelectedCustomerChanged(CustomerResponse? value) => MarkDirty();

    private void MarkDirty()
    {
        if (_trackDirty)
            IsDirty = true;
    }

    partial void OnEventIdChanged(string? value)
    {
        if (Guid.TryParse(value, out _))
        {
            IsNew = false;
            IsEditing = true;
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
        {
            _trackDirty = true;
            IsDirty = false;
            LoadPickerDataCommand.Execute(null);
        }
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
                IsAllDay = false;
                EndDate = evt.EndTime.Value.ToLocalTime().Date;
                EndTime = evt.EndTime.Value.ToLocalTime().TimeOfDay;
            }
            else
            {
                IsAllDay = true;
            }

            AssignedUsers = new ObservableCollection<UserBriefResponse>(
                evt.Assignments.Select(a => new UserBriefResponse { Id = a.UserId, Name = a.Name }));

            await LoadPickerDataAsync();
            if (evt.CustomerId.HasValue)
                SelectedCustomer = Customers.FirstOrDefault(c => c.Id == evt.CustomerId.Value);

            _trackDirty = true;
            IsDirty = false;
        });
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

        try
        {
            var startDateTime = StartDate.Date.Add(StartTime);
            startDateTime = DateTime.SpecifyKind(startDateTime, DateTimeKind.Local).ToUniversalTime();
            DateTime? endDateTime = !IsAllDay
                ? DateTime.SpecifyKind(EndDate.Date.Add(EndTime), DateTimeKind.Local).ToUniversalTime()
                : null;

            await LoadAsync(async () =>
            {
                if (IsNew)
                {
                    var request = new CreateEventRequest
                    {
                        Title = Title.Trim(),
                        Description = string.IsNullOrWhiteSpace(Description) ? null : Description.Trim(),
                        StartTime = startDateTime,
                        EndTime = endDateTime,
                        CustomerId = SelectedCustomer?.Id,
                        AssignedUserIds = AssignedUsers?.Select(u => u.Id).ToList() ?? []
                    };
                    await _apiService.CreateEventAsync(request);
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
                }
            });

            if (!HasError)
            {
                IsDirty = false;
                if (IsNew)
                    await Shell.Current.GoToAsync("..");
            }
        }
        catch (Exception ex)
        {
            var path = Path.Combine(FileSystem.AppDataDirectory, "crash.log");
            File.WriteAllText(path, $"{DateTime.Now}\n{ex}\n");
            ErrorMessage = $"{ex.GetType().Name}: {ex.Message}\nat {ex.StackTrace?.Split('\n').FirstOrDefault()}";
            HasError = true;
        }
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
        await Shell.Current.GoToAsync("..");
    }

    [RelayCommand]
    private async Task GoBackAsync()
    {
        if (MainLayout.Current?.IsWideLayout == true)
        {
            WeakReferenceMessenger.Default.Send(new ShowDetailMessage(new DetailRequest
            {
                Route = "daySummary",
                Properties = new()
                {
                    ["SelectedDate"] = StartDate,
                }
            }));
        }
        else
        {
            await Shell.Current.GoToAsync("..");
        }
    }

    [RelayCommand]
    private void AddAssignment(UserBriefResponse? user)
    {
        if (user == null) return;
        if (!AssignedUsers.Any(u => u.Id == user.Id))
        {
            AssignedUsers.Add(user);
            MarkDirty();
        }
    }

    [RelayCommand]
    private void RemoveAssignment(UserBriefResponse user)
    {
        var existing = AssignedUsers.FirstOrDefault(u => u.Id == user.Id);
        if (existing != null)
        {
            AssignedUsers.Remove(existing);
            MarkDirty();
        }
    }
}
