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
    private const string CacheKey = "jobs";

    private readonly ApiService _apiService;
    private readonly ListCacheService _listCache;

    // Full dataset; Jobs below is the (possibly search-filtered) view of it.
    private List<JobListItemResponse> _allJobs = new();

    [ObservableProperty]
    private ObservableCollection<JobListItemResponse> _jobs = new();

    [ObservableProperty]
    private string _searchText = string.Empty;

    [ObservableProperty]
    private JobListItemResponse? _selectedJob;

    private string? _pendingSelectId;
    private bool _suppressSelectionNav;

    public event Action<JobListItemResponse>? ScrollToRequested;

    public JobsListViewModel(ApiService apiService, ListCacheService listCache)
    {
        _apiService = apiService;
        _listCache = listCache;

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
        // First load: show the last-known data from disk instantly, then let the
        // network refresh below merge in whatever changed.
        if (_allJobs.Count == 0 && !IsBusy)
        {
            var cached = await _listCache.LoadAsync<JobListItemResponse>(CacheKey);
            if (cached is { Count: > 0 } && _allJobs.Count == 0)
            {
                _allJobs = cached;
                PublishList(rebuild: true);
                if (TrySelectPending()) _pendingSelectId = null;
            }
        }

        await LoadAsync(async () =>
        {
            _allJobs = await _apiService.GetAllJobsAsync();
            PublishList(rebuild: false);
            if (TrySelectPending()) _pendingSelectId = null;
            _ = _listCache.SaveAsync(CacheKey, _allJobs);
        }, showLoading: Jobs.Count == 0);
    }

    // Projects the master list through the current search filter into the bound
    // collection. rebuild swaps the collection wholesale (right for filter changes,
    // where most rows differ); otherwise rows are merged in place so only actual
    // changes re-render.
    private void PublishList(bool rebuild)
    {
        var query = SearchText.Trim();
        var visible = query.Length == 0
            ? _allJobs
            : _allJobs.Where(j => MatchesSearch(j, query)).ToList();

        if (rebuild || Jobs.Count == 0 || visible.Count == 0)
        {
            Jobs = new ObservableCollection<JobListItemResponse>(visible);
        }
        else
        {
            var selectedId = SelectedJob?.Id;
            Jobs.MergeInto(visible, j => j.Id, RowUnchanged);
            ReselectById(selectedId);
        }

        if (Jobs.Count == 0) SetEmpty();
        else SetContent();
    }

    private static bool MatchesSearch(JobListItemResponse j, string query) =>
        j.Title.Contains(query, StringComparison.OrdinalIgnoreCase)
        || j.CustomerName.Contains(query, StringComparison.OrdinalIgnoreCase);

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

    // Search filters the in-memory list — instant, no debounce, no network.
    partial void OnSearchTextChanged(string value) => PublishList(rebuild: true);

    [RelayCommand]
    private void Search() => PublishList(rebuild: true);

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
        // The tap gesture consumes the touch before native selection happens —
        // select here so the list shows the indicator.
        SelectedJob = job;
        var id = job.Id.ToString();
        WeakReferenceMessenger.Default.Send(new ShowDetailMessage(new DetailRequest
        {
            Route = "jobDetail",
            Properties = new() { ["JobId"] = id },
            QueryParams = new() { ["id"] = id }
        }));
    }
}
