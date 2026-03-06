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
    private string _scopeNotes = string.Empty;

    [ObservableProperty]
    private ObservableCollection<CustomerResponse> _customers = new();

    [ObservableProperty]
    private CustomerResponse? _selectedCustomer;

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

    [RelayCommand]
    private async Task LoadDataAsync()
    {
        await LoadAsync(async () =>
        {
            if (IsNew)
            {
                var result = await _apiService.GetCustomersAsync(pageSize: 100);
                Customers = new ObservableCollection<CustomerResponse>(result.Items);
                if (Guid.TryParse(InitialCustomerId, out var custId))
                    SelectedCustomer = Customers.FirstOrDefault(c => c.Id == custId);
            }
            else if (Guid.TryParse(JobId, out var jobId))
            {
                var job = await _apiService.GetJobAsync(jobId);
                if (job != null)
                {
                    Title = job.Title;
                    SelectedStatus = job.Status;
                    SelectedPriority = job.Priority;
                    ScopeNotes = job.ScopeNotes ?? string.Empty;
                }
            }
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
