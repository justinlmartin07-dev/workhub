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
    private readonly AuthService _authService;

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

    // Active sort: "priority" / "status" / null for API order. Lives on the
    // singleton VM so it survives tab switches; resets on app restart.
    public string? SortKey { get; private set; }
    public bool SortAscending { get; private set; }

    public event Action<JobListItemResponse>? ScrollToRequested;

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

    public JobsListViewModel(ApiService apiService, ListCacheService listCache, AuthService authService)
    {
        _apiService = apiService;
        _listCache = listCache;
        _authService = authService;
        _userPhotoUrl = authService.CurrentUser?.ProfilePhotoUrl;

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
            else if (m.Value == "user_photo")
                MainThread.BeginInvokeOnMainThread(() => UserPhotoUrl = _authService.CurrentUser?.ProfilePhotoUrl);
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
    // collection. rebuild swaps the collection wholesale; otherwise rows are merged
    // in place so only actual changes re-render. visible may be pre-computed off
    // the main thread by the search debounce path.
    private void PublishList(bool rebuild, IReadOnlyList<JobListItemResponse>? visible = null)
    {
        visible ??= ProjectList(_allJobs, SearchText.Trim());

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

    // Filter + sort in one pass; safe to run off the main thread on a snapshot.
    private List<JobListItemResponse> ProjectList(List<JobListItemResponse> source, string query)
    {
        IEnumerable<JobListItemResponse> result = query.Length == 0
            ? source
            : source.Where(j => MatchesSearch(j, query));

        result = SortKey switch
        {
            "priority" => SortAscending
                ? result.OrderBy(j => PriorityRank(j.Priority))
                : result.OrderByDescending(j => PriorityRank(j.Priority)),
            "status" => SortAscending
                ? result.OrderBy(j => StatusRank(j.Status))
                : result.OrderByDescending(j => StatusRank(j.Status)),
            _ => result,
        };
        return result.ToList();
    }

    private static int PriorityRank(string p) => p switch
    {
        "Low" => 0,
        "Medium" => 1,
        "High" => 2,
        _ => -1,
    };

    // Most-active-first order, so ascending puts work underway above untouched
    // jobs and descending puts finished work first.
    private static int StatusRank(string s) => s switch
    {
        "In Progress" => 0,
        "New" => 1,
        "On Hold" => 2,
        "Complete" => 3,
        "Cancelled" => 4,
        _ => -1,
    };

    public void SetSort(string? key, bool ascending)
    {
        SortKey = key;
        SortAscending = ascending;
        PublishList(rebuild: true);
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

    private CancellationTokenSource? _searchCts;

    partial void OnSearchTextChanged(string value)
    {
        _searchCts?.Cancel();
        _searchCts = new CancellationTokenSource();
        _ = DebounceSearchAsync(_searchCts.Token);
    }

    private async Task DebounceSearchAsync(CancellationToken ct)
    {
        try { await Task.Delay(200, ct); }
        catch (OperationCanceledException) { return; }
        var query = SearchText.Trim();
        var snapshot = _allJobs;
        var visible = await Task.Run(() => ProjectList(snapshot, query), ct);
        if (ct.IsCancellationRequested) return;
        PublishList(rebuild: false, visible);
    }

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
