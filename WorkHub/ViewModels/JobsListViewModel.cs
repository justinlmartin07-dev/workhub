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
    private JobListItemResponse? _selectedJob;

    private string? _pendingSelectId;
    private CancellationTokenSource? _searchCts;
    private bool _suppressSelectionNav;

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

        WeakReferenceMessenger.Default.Register<DataChangedMessage>(this, (r, m) =>
        {
            if (m.Value == "job")
                MainThread.BeginInvokeOnMainThread(() => LoadJobsCommand.Execute(null));
        });
    }

    [RelayCommand]
    public async Task LoadJobsAsync()
    {
        await LoadAsync(async () =>
        {
            var all = new List<JobListItemResponse>();
            var page = 1;
            int totalPages;
            do
            {
                var result = await _apiService.GetJobsAsync(SearchText, null, null, page: page);
                totalPages = result.TotalPages;
                all.AddRange(result.Items);
                page++;
            } while (page <= totalPages);

            if (Jobs.Count == 0)
            {
                Jobs = new ObservableCollection<JobListItemResponse>(all);
            }
            else
            {
                var selectedId = SelectedJob?.Id;
                Jobs.MergeInto(all, j => j.Id, RowUnchanged);
                ReselectById(selectedId);
            }

            if (Jobs.Count == 0) SetEmpty();
            else SetContent();
            if (TrySelectPending()) _pendingSelectId = null;
        }, showLoading: Jobs.Count == 0);
    }

    private static bool RowUnchanged(JobListItemResponse a, JobListItemResponse b) =>
        a.Title == b.Title
        && a.CustomerName == b.CustomerName
        && a.CustomerId == b.CustomerId
        && a.Status == b.Status
        && a.Priority == b.Priority
        && a.CreatedAt == b.CreatedAt;

    // After a merge replaced the selected item with a fresh instance, re-point the
    // selection at the new instance without re-triggering detail navigation.
    private void ReselectById(Guid? selectedId)
    {
        if (selectedId == null || SelectedJob?.Id == selectedId) return;
        var match = Jobs.FirstOrDefault(j => j.Id == selectedId);
        if (match == null) return;
        _suppressSelectionNav = true;
        try { SelectedJob = match; }
        finally { _suppressSelectionNav = false; }
    }

    private bool TrySelectPending()
    {
        if (_pendingSelectId == null || Jobs.Count == 0) return false;
        if (!Guid.TryParse(_pendingSelectId, out var id))
        {
            SelectedJob = null;
            _pendingSelectId = null;
            return false;
        }

        var match = Jobs.FirstOrDefault(j => j.Id == id);
        if (match != null)
        {
            // Select without navigating — the sender of SelectListItemMessage
            // shows the detail itself; this is just list highlight + scroll.
            _suppressSelectionNav = true;
            try { SelectedJob = match; }
            finally { _suppressSelectionNav = false; }
            ScrollToRequested?.Invoke(match);
            return true;
        }
        return false;
    }

    partial void OnSearchTextChanged(string value)
    {
        _searchCts?.Cancel();
        _searchCts = new CancellationTokenSource();
        var token = _searchCts.Token;
        MainThread.BeginInvokeOnMainThread(async () =>
        {
            try
            {
                await Task.Delay(300, token);
                await LoadJobsAsync();
            }
            catch (TaskCanceledException) { }
        });
    }

    [RelayCommand]
    private async Task SearchAsync()
    {
        _searchCts?.Cancel();
        await LoadJobsAsync();
    }

    [RelayCommand]
    private void AddJob()
    {
        WeakReferenceMessenger.Default.Send(new ShowDetailMessage(new DetailRequest
        {
            Route = "jobEdit",
            QueryParams = new()
        }));
    }

    [RelayCommand]
    private void SelectJob(JobListItemResponse job)
    {
        if (job == null || _suppressSelectionNav) return;
        var id = job.Id.ToString();
        WeakReferenceMessenger.Default.Send(new ShowDetailMessage(new DetailRequest
        {
            Route = "jobDetail",
            Properties = new() { ["JobId"] = id },
            QueryParams = new() { ["id"] = id }
        }));
    }
}
