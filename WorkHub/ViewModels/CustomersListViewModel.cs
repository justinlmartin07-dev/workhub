using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using CommunityToolkit.Mvvm.Messaging;
using WorkHub.Messages;
using WorkHub.Models;
using WorkHub.Services;

namespace WorkHub.ViewModels;

public partial class CustomersListViewModel : BaseViewModel
{
    private const string CacheKey = "customers";

    private readonly ApiService _apiService;
    private readonly ListCacheService _listCache;
    private readonly AuthService _authService;

    // Full dataset; Customers below is the (possibly search-filtered) view of it.
    private List<CustomerResponse> _allCustomers = new();

    [ObservableProperty]
    private ObservableCollection<CustomerResponse> _customers = new();

    [ObservableProperty]
    private string _searchText = string.Empty;

    [ObservableProperty]
    private CustomerResponse? _selectedCustomer;

    private string? _pendingSelectId;
    private bool _suppressSelectionNav;

    public event Action<CustomerResponse>? ScrollToRequested;

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

    public CustomersListViewModel(ApiService apiService, ListCacheService listCache, AuthService authService)
    {
        _apiService = apiService;
        _listCache = listCache;
        _authService = authService;
        _userPhotoUrl = authService.CurrentUser?.ProfilePhotoUrl;

        WeakReferenceMessenger.Default.Register<SelectListItemMessage>(this, (r, m) =>
        {
            if (m.Value.TabIndex != 0) return; // Only handle Customers tab
            _pendingSelectId = m.Value.ItemId;
            TrySelectPending();
        });

        WeakReferenceMessenger.Default.Register<DataChangedMessage>(this, (r, m) =>
        {
            if (m.Value == "customer")
                MainThread.BeginInvokeOnMainThread(() => LoadCustomersCommand.Execute(null));
            else if (m.Value == "user_photo")
                MainThread.BeginInvokeOnMainThread(() => UserPhotoUrl = _authService.CurrentUser?.ProfilePhotoUrl);
        });
    }

    protected override Task OnRefreshRequestedAsync() => LoadCustomersAsync();

    [RelayCommand]
    public async Task LoadCustomersAsync()
    {
        // First load: show the last-known data from disk instantly, then let the
        // network refresh below merge in whatever changed.
        if (_allCustomers.Count == 0 && !IsBusy)
        {
            var cached = await _listCache.LoadAsync<CustomerResponse>(CacheKey);
            if (cached is { Count: > 0 } && _allCustomers.Count == 0)
            {
                _allCustomers = cached;
                PublishList(rebuild: true);
                if (TrySelectPending()) _pendingSelectId = null;
            }
        }

        await LoadAsync(async () =>
        {
            _allCustomers = await _apiService.GetAllCustomersAsync();
            PublishList(rebuild: false);
            if (TrySelectPending()) _pendingSelectId = null;
            _ = _listCache.SaveAsync(CacheKey, _allCustomers);
        }, showLoading: Customers.Count == 0);
    }

    // Projects the master list through the current search filter into the bound
    // collection. rebuild swaps the collection wholesale; otherwise rows are merged
    // in place so only actual changes re-render. visible may be pre-computed off
    // the main thread by the search debounce path.
    private void PublishList(bool rebuild, IReadOnlyList<CustomerResponse>? visible = null, bool followSelection = true)
    {
        if (visible == null)
        {
            var query = SearchText.Trim();
            visible = query.Length == 0
                ? _allCustomers
                : _allCustomers.Where(c => MatchesSearch(c, query)).ToList();
        }

        if (rebuild || Customers.Count == 0 || visible.Count == 0)
        {
            Customers = new ObservableCollection<CustomerResponse>(visible);
        }
        else
        {
            var selectedId = SelectedCustomer?.Id;
            var oldIndex = IndexOfId(selectedId);
            Customers.MergeInto(visible, c => c.Id, RowUnchanged);
            ReselectById(selectedId);

            // Follow the selected row when a refresh resorted it to a new spot
            // (e.g. the customer was renamed and the list is ordered by name).
            if (followSelection && SelectedCustomer != null && oldIndex >= 0)
            {
                var newIndex = Customers.IndexOf(SelectedCustomer);
                if (newIndex >= 0 && newIndex != oldIndex)
                    ScrollToRequested?.Invoke(SelectedCustomer);
            }
        }

        if (Customers.Count == 0) SetEmpty();
        else SetContent();
    }

    private int IndexOfId(Guid? id)
    {
        if (id == null) return -1;
        for (int i = 0; i < Customers.Count; i++)
            if (Customers[i].Id == id) return i;
        return -1;
    }

    private static bool MatchesSearch(CustomerResponse c, string query) =>
        c.Name.Contains(query, StringComparison.OrdinalIgnoreCase)
        || (c.Address?.Contains(query, StringComparison.OrdinalIgnoreCase) ?? false)
        || (c.Contacts?.Any(ct => ct.Value.Contains(query, StringComparison.OrdinalIgnoreCase)) ?? false);

    private static bool RowUnchanged(CustomerResponse a, CustomerResponse b) =>
        a.Name == b.Name
        && a.Address == b.Address
        && a.UpdatedAt == b.UpdatedAt
        && a.PrimaryPhone == b.PrimaryPhone
        && a.PrimaryEmail == b.PrimaryEmail;

    // After a merge replaced the selected item with a fresh instance, re-point the
    // selection at the new instance without re-triggering detail navigation.
    private void ReselectById(Guid? selectedId)
    {
        if (selectedId == null || SelectedCustomer?.Id == selectedId) return;
        var match = Customers.FirstOrDefault(c => c.Id == selectedId);
        if (match == null) return;
        _suppressSelectionNav = true;
        try { SelectedCustomer = match; }
        finally { _suppressSelectionNav = false; }
    }

    private bool TrySelectPending()
    {
        if (_pendingSelectId == null || Customers.Count == 0) return false;
        if (!Guid.TryParse(_pendingSelectId, out var id))
        {
            SelectedCustomer = null;
            _pendingSelectId = null;
            return false;
        }

        var match = Customers.FirstOrDefault(c => c.Id == id);
        if (match != null)
        {
            // Select without navigating — the sender of SelectListItemMessage
            // shows the detail itself; this is just list highlight + scroll.
            _suppressSelectionNav = true;
            try { SelectedCustomer = match; }
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
        var snapshot = _allCustomers;
        var visible = await Task.Run(() =>
            query.Length == 0 ? snapshot : snapshot.Where(c => MatchesSearch(c, query)).ToList(), ct);
        if (ct.IsCancellationRequested) return;
        // Filtering isn't a data change — don't yank the view to the selected
        // row while the user is typing a search.
        PublishList(rebuild: false, visible, followSelection: false);
    }

    [RelayCommand]
    private void Search() => PublishList(rebuild: true);

    [RelayCommand]
    private void AddCustomer()
    {
        WeakReferenceMessenger.Default.Send(new ShowDetailMessage(new DetailRequest
        {
            Route = "customerEdit",
            QueryParams = new()
        }));
    }

    [RelayCommand]
    private void SelectCustomer(CustomerResponse customer)
    {
        if (customer == null || _suppressSelectionNav) return;
        SelectedCustomer = customer;
        var id = customer.Id.ToString();
        WeakReferenceMessenger.Default.Send(new ShowDetailMessage(new DetailRequest
        {
            Route = "customerDetail",
            Properties = new() { ["CustomerId"] = id },
            QueryParams = new() { ["id"] = id }
        }));
    }
}
