using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using CommunityToolkit.Mvvm.Messaging;
using WorkHub.Messages;
using WorkHub.Models;
using WorkHub.Services;

namespace WorkHub.ViewModels;

public partial class JobsListViewModel : BaseViewModel
{
    private readonly ApiService _apiService;

    [ObservableProperty]
    private ObservableCollection<JobListItemResponse> _jobs = new();

    [ObservableProperty]
    private string _searchText = string.Empty;

    [ObservableProperty]
    private string? _statusFilter;

    [ObservableProperty]
    private string? _priorityFilter;

    [ObservableProperty]
    private JobListItemResponse? _selectedJob;

    private string? _pendingSelectId;

    public List<string> StatusOptions { get; } = new() { "", "new", "in_progress", "on_hold", "complete", "cancelled" };
    public List<string> PriorityOptions { get; } = new() { "", "low", "medium", "high" };

    public event Action<JobListItemResponse>? ScrollToRequested;

    public JobsListViewModel(ApiService apiService)
    {
        _apiService = apiService;

        WeakReferenceMessenger.Default.Register<SelectListItemMessage>(this, (r, m) =>
        {
            if (m.Value.TabIndex != 1) return; // Only handle Jobs tab
            _pendingSelectId = m.Value.ItemId;
            TrySelectPending();
        });
    }

    [RelayCommand]
    public async Task LoadJobsAsync()
    {
        await LoadAsync(async () =>
        {
            var status = string.IsNullOrEmpty(StatusFilter) ? null : StatusFilter;
            var priority = string.IsNullOrEmpty(PriorityFilter) ? null : PriorityFilter;
            var all = new List<JobListItemResponse>();
            var page = 1;
            int totalPages;
            do
            {
                var result = await _apiService.GetJobsAsync(SearchText, status, priority, page: page);
                totalPages = result.TotalPages;
                all.AddRange(result.Items);
                page++;
            } while (page <= totalPages);

            Jobs = new ObservableCollection<JobListItemResponse>(all);
            if (Jobs.Count == 0) SetEmpty();
            else SetContent();
            TrySelectPending();
        });
    }

    private void TrySelectPending()
    {
        if (_pendingSelectId == null || Jobs.Count == 0) return;
        if (!Guid.TryParse(_pendingSelectId, out var id)) return;

        var match = Jobs.FirstOrDefault(j => j.Id == id);
        if (match != null)
        {
            SelectedJob = match;
            ScrollToRequested?.Invoke(match);
            _pendingSelectId = null;
        }
    }

    [RelayCommand]
    private async Task SearchAsync()
    {
        await LoadJobsAsync();
    }

    [RelayCommand]
    private async Task FilterChangedAsync()
    {
        await LoadJobsAsync();
    }

    [RelayCommand]
    private async Task AddJobAsync()
    {
        await Shell.Current.GoToAsync("jobEdit");
    }

    [RelayCommand]
    private void SelectJob(JobListItemResponse job)
    {
        if (job == null) return;
        var id = job.Id.ToString();
        WeakReferenceMessenger.Default.Send(new ShowDetailMessage(new DetailRequest
        {
            Route = "jobDetail",
            Properties = new() { ["JobId"] = id },
            QueryParams = new() { ["id"] = id }
        }));
    }
}
