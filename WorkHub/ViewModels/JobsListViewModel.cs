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

    private int _currentPage = 1;
    private int _totalPages = 1;

    public List<string> StatusOptions { get; } = new() { "", "new", "in_progress", "on_hold", "complete", "cancelled" };
    public List<string> PriorityOptions { get; } = new() { "", "low", "medium", "high" };

    public JobsListViewModel(ApiService apiService)
    {
        _apiService = apiService;
    }

    [RelayCommand]
    public async Task LoadJobsAsync()
    {
        _currentPage = 1;
        await LoadAsync(async () =>
        {
            var result = await _apiService.GetJobsAsync(SearchText,
                string.IsNullOrEmpty(StatusFilter) ? null : StatusFilter,
                string.IsNullOrEmpty(PriorityFilter) ? null : PriorityFilter,
                page: _currentPage);
            _totalPages = result.TotalPages;
            Jobs = new ObservableCollection<JobListItemResponse>(result.Items);
            if (Jobs.Count == 0) SetEmpty();
            else SetContent();
        });
    }

    [RelayCommand]
    private async Task LoadMoreAsync()
    {
        if (IsBusy || _currentPage >= _totalPages) return;
        _currentPage++;
        await LoadAsync(async () =>
        {
            var result = await _apiService.GetJobsAsync(SearchText, StatusFilter, PriorityFilter, page: _currentPage);
            foreach (var job in result.Items)
                Jobs.Add(job);
        }, showLoading: false);
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
