using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using WorkHub.Models;
using WorkHub.Services;

namespace WorkHub.ViewModels;

[QueryProperty(nameof(JobId), "id")]
[QueryProperty(nameof(InitialCustomerId), "customerId")]
public partial class JobEditViewModel : BaseViewModel
{
    private readonly ApiService _apiService;

    [ObservableProperty]
    private string? _jobId;

    [ObservableProperty]
    private string? _initialCustomerId;

    [ObservableProperty]
    private string _title = string.Empty;

    [ObservableProperty]
    private string _selectedStatus = "new";

    [ObservableProperty]
    private string _selectedPriority = "medium";

    [ObservableProperty]
    private double _prioritySliderValue = 1;

    partial void OnPrioritySliderValueChanged(double value)
    {
        SelectedPriority = (int)Math.Round(value) switch
        {
            0 => "low",
            1 => "medium",
            2 => "high",
            _ => "medium"
        };
    }

    [ObservableProperty]
    private string _scopeNotes = string.Empty;

    [ObservableProperty]
    private ObservableCollection<CustomerResponse> _allCustomers = new();

    [ObservableProperty]
    private ObservableCollection<CustomerResponse> _filteredCustomers = new();

    [ObservableProperty]
    private CustomerResponse? _selectedCustomer;

    [ObservableProperty]
    private string _customerSearchText = string.Empty;

    [ObservableProperty]
    private bool _isCustomerPickerOpen;

    [ObservableProperty]
    private bool _isNew = true;

    [ObservableProperty]
    private string _pageTitle = "New Job";

    public List<string> StatusOptions { get; } = new() { "new", "in_progress", "on_hold", "complete", "cancelled" };
    public List<string> PriorityOptions { get; } = new() { "low", "medium", "high" };

    public JobEditViewModel(ApiService apiService)
    {
        _apiService = apiService;
    }

    partial void OnJobIdChanged(string? value)
    {
        if (Guid.TryParse(value, out _))
        {
            IsNew = false;
            PageTitle = "Edit Job";
            LoadDataCommand.Execute(null);
        }
    }

    partial void OnInitialCustomerIdChanged(string? value)
    {
        if (IsNew)
            LoadDataCommand.Execute(null);
    }

    partial void OnCustomerSearchTextChanged(string value)
    {
        FilterCustomers();
    }

    private void FilterCustomers()
    {
        if (string.IsNullOrWhiteSpace(CustomerSearchText))
        {
            FilteredCustomers = new ObservableCollection<CustomerResponse>(AllCustomers);
        }
        else
        {
            var search = CustomerSearchText.ToLower();
            FilteredCustomers = new ObservableCollection<CustomerResponse>(
                AllCustomers.Where(c =>
                    c.Name.ToLower().Contains(search) ||
                    (c.Phone?.ToLower().Contains(search) ?? false) ||
                    (c.Email?.ToLower().Contains(search) ?? false) ||
                    (c.Address?.ToLower().Contains(search) ?? false)));
        }
    }

    [RelayCommand]
    private async Task LoadDataAsync()
    {
        await LoadAsync(async () =>
        {
            if (IsNew)
            {
                await LoadAllCustomersAsync();
                if (Guid.TryParse(InitialCustomerId, out var custId))
                    SelectedCustomer = AllCustomers.FirstOrDefault(c => c.Id == custId);
            }
            else if (Guid.TryParse(JobId, out var jobId))
            {
                var job = await _apiService.GetJobAsync(jobId);
                if (job != null)
                {
                    Title = job.Title;
                    SelectedStatus = job.Status;
                    SelectedPriority = job.Priority;
                    PrioritySliderValue = job.Priority switch { "low" => 0, "high" => 2, _ => 1 };
                    ScopeNotes = job.ScopeNotes ?? string.Empty;
                }
            }
        });
    }

    private async Task LoadAllCustomersAsync()
    {
        var all = new List<CustomerResponse>();
        int page = 1;
        int totalPages;
        do
        {
            var result = await _apiService.GetCustomersAsync(page: page, pageSize: 100);
            all.AddRange(result.Items);
            totalPages = result.TotalPages;
            page++;
        } while (page <= totalPages);

        AllCustomers = new ObservableCollection<CustomerResponse>(all.OrderBy(c => c.Name));
        FilterCustomers();
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
            if (IsNew)
            {
                if (SelectedCustomer == null)
                {
                    throw new Exception("Please select a customer");
                }
                var request = new CreateJobRequest
                {
                    CustomerId = SelectedCustomer.Id,
                    Title = Title.Trim(),
                    Status = SelectedStatus,
                    Priority = SelectedPriority,
                    ScopeNotes = string.IsNullOrWhiteSpace(ScopeNotes) ? null : ScopeNotes.Trim()
                };
                await _apiService.CreateJobAsync(request);
            }
            else
            {
                var request = new UpdateJobRequest
                {
                    Title = Title.Trim(),
                    Status = SelectedStatus,
                    Priority = SelectedPriority,
                    ScopeNotes = string.IsNullOrWhiteSpace(ScopeNotes) ? null : ScopeNotes.Trim()
                };
                await _apiService.UpdateJobAsync(Guid.Parse(JobId!), request);
            }
            await Shell.Current.GoToAsync("..");
        });
    }

    [RelayCommand]
    private async Task CancelAsync()
    {
        await Shell.Current.GoToAsync("..");
    }
}
