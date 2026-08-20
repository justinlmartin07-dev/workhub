using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using CommunityToolkit.Mvvm.Messaging;
using WorkHub.Messages;
using WorkHub.Models;
using WorkHub.Services;

namespace WorkHub.ViewModels;

public partial class OrdersViewModel : BaseViewModel
{
    private const string CacheKey = "orders";

    private readonly ApiService _apiService;
    private readonly ListCacheService _listCache;
    private readonly AuthService _authService;

    // Full dataset; Orders below is the (possibly search-filtered) view of it.
    private List<OrderLineResponse> _allOrders = new();

    [ObservableProperty]
    private ObservableCollection<OrderLineResponse> _orders = new();

    public int OutstandingCount => _allOrders.Count(o => !o.IsOrdered);
    public int OrderedCount => _allOrders.Count(o => o.IsOrdered);

    [ObservableProperty]
    private string _searchText = string.Empty;

    [ObservableProperty]
    private OrderLineResponse? _selectedOrder;

    public event Action<OrderLineResponse>? ScrollToRequested;

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

    public OrdersViewModel(ApiService apiService, ListCacheService listCache, AuthService authService)
    {
        _apiService = apiService;
        _listCache = listCache;
        _authService = authService;
        _userPhotoUrl = authService.CurrentUser?.ProfilePhotoUrl;

        // Adding/removing to-order parts (or job changes) needs a fresh list.
        WeakReferenceMessenger.Default.Register<DataChangedMessage>(this, (r, m) =>
        {
            if (m.Value is "orders" or "job")
                MainThread.BeginInvokeOnMainThread(() => LoadOrdersCommand.Execute(null));
            else if (m.Value == "user_photo")
                MainThread.BeginInvokeOnMainThread(() => UserPhotoUrl = _authService.CurrentUser?.ProfilePhotoUrl);
        });

        // Marking a single part ordered updates just that row — no network reload.
        WeakReferenceMessenger.Default.Register<OrderOrderedChangedMessage>(this, (r, m) =>
        {
            MainThread.BeginInvokeOnMainThread(() => ApplyOrderedChange(m.Value));
        });
    }

    private void ApplyOrderedChange(OrderOrderedChange change)
    {
        var item = _allOrders.FirstOrDefault(o => o.Id == change.ItemId && o.Source == change.Source);
        if (item == null)
        {
            OnPropertyChanged(nameof(OutstandingCount));
            OnPropertyChanged(nameof(OrderedCount));
            return;
        }

        item.OrderedAt = change.OrderedAt;
        // Re-sort locally the same way the API does so the row lands in its
        // proper new spot immediately instead of on the next reload. The merge
        // moves it in place and the follow-selection scroll keeps it in view.
        _allOrders = SortLikeApi(_allOrders);
        PublishList(rebuild: false);
    }

    // Mirrors OrdersController's ordering: outstanding first, ordered last,
    // then customer / job / part name.
    private static List<OrderLineResponse> SortLikeApi(IEnumerable<OrderLineResponse> orders) =>
        orders
            .OrderBy(o => o.OrderedAt.HasValue)
            .ThenBy(o => o.CustomerName)
            .ThenBy(o => o.JobTitle)
            .ThenBy(o => o.Name)
            .ToList();

    protected override Task OnRefreshRequestedAsync() => LoadOrdersAsync();

    [RelayCommand]
    public async Task LoadOrdersAsync()
    {
        // First load: show the last-known data from disk instantly, then let the
        // network refresh below merge in whatever changed.
        if (_allOrders.Count == 0 && !IsBusy)
        {
            var cached = await _listCache.LoadAsync<OrderLineResponse>(CacheKey);
            if (cached is { Count: > 0 } && _allOrders.Count == 0)
            {
                _allOrders = cached;
                PublishList(rebuild: true);
            }
        }

        await LoadAsync(async () =>
        {
            var orders = await _apiService.GetOrdersAsync();
            _allOrders = orders;
            PublishList(rebuild: false);
            _ = _listCache.SaveAsync(CacheKey, orders);
        }, showLoading: Orders.Count == 0);
    }

    // Projects the master list through the current search filter into the bound
    // collection. rebuild swaps the collection wholesale; otherwise rows are merged
    // in place so only actual changes re-render. visible may be pre-computed off
    // the main thread by the search debounce path.
    private void PublishList(bool rebuild, IReadOnlyList<OrderLineResponse>? visible = null, bool followSelection = true)
    {
        if (visible == null)
        {
            var query = SearchText.Trim();
            visible = query.Length == 0
                ? _allOrders
                : _allOrders.Where(o => MatchesSearch(o, query)).ToList();
        }

        if (rebuild || Orders.Count == 0)
        {
            Orders = new ObservableCollection<OrderLineResponse>(visible);
        }
        else
        {
            var selectedKey = SelectedOrder is { } s ? ((Guid, string)?)(s.Id, s.Source) : null;
            var oldIndex = IndexOfKey(selectedKey);
            Orders.MergeInto(visible, o => (o.Id, o.Source), RowUnchanged, TryUpdateInPlace);
            ReselectByKey(selectedKey);

            // Follow the selected row when a refresh resorted it (marking a
            // part ordered moves it on the next reload from the API).
            if (followSelection && SelectedOrder != null && oldIndex >= 0)
            {
                var newIndex = Orders.IndexOf(SelectedOrder);
                if (newIndex >= 0 && newIndex != oldIndex)
                    ScrollToRequested?.Invoke(SelectedOrder);
            }
        }

        OnPropertyChanged(nameof(OutstandingCount));
        OnPropertyChanged(nameof(OrderedCount));

        if (_allOrders.Count == 0) SetEmpty();
        else SetContent();
    }

    private int IndexOfKey((Guid Id, string Source)? key)
    {
        if (key == null) return -1;
        for (int i = 0; i < Orders.Count; i++)
            if (Orders[i].Id == key.Value.Id && Orders[i].Source == key.Value.Source) return i;
        return -1;
    }

    // After a merge replaced the selected row with a fresh instance, native
    // selection drops (SelectedOrder goes null via the TwoWay binding) —
    // re-point it at the new instance.
    private void ReselectByKey((Guid Id, string Source)? key)
    {
        if (key == null || SelectedOrder != null) return;
        var match = Orders.FirstOrDefault(o => o.Id == key.Value.Id && o.Source == key.Value.Source);
        if (match != null) SelectedOrder = match;
    }

    private static bool MatchesSearch(OrderLineResponse o, string query) =>
        o.Name.Contains(query, StringComparison.OrdinalIgnoreCase)
        || (o.PartNumber?.Contains(query, StringComparison.OrdinalIgnoreCase) ?? false)
        || (o.Sku?.Contains(query, StringComparison.OrdinalIgnoreCase) ?? false)
        || (o.Description?.Contains(query, StringComparison.OrdinalIgnoreCase) ?? false)
        || o.JobTitle.Contains(query, StringComparison.OrdinalIgnoreCase)
        || o.CustomerName.Contains(query, StringComparison.OrdinalIgnoreCase);

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
        var snapshot = _allOrders;
        var visible = await Task.Run(() =>
            query.Length == 0 ? snapshot : snapshot.Where(o => MatchesSearch(o, query)).ToList(), ct);
        if (ct.IsCancellationRequested) return;
        // Filtering isn't a data change — don't yank the view to the selected
        // row while the user is typing a search.
        PublishList(rebuild: false, visible, followSelection: false);
    }

    [RelayCommand]
    private void Search() => PublishList(rebuild: true);

    private static bool RowUnchanged(OrderLineResponse a, OrderLineResponse b) =>
        FieldsUnchanged(a, b) && a.OrderedAt == b.OrderedAt;

    private static bool FieldsUnchanged(OrderLineResponse a, OrderLineResponse b) =>
        a.Name == b.Name
        && a.Description == b.Description
        && a.PartNumber == b.PartNumber
        && a.Sku == b.Sku
        && a.Quantity == b.Quantity
        && a.JobId == b.JobId
        && a.JobTitle == b.JobTitle
        && a.CustomerId == b.CustomerId
        && a.CustomerName == b.CustomerName;

    // OrderedAt is observable, so when it's the only difference update the existing
    // row in place instead of replacing it (avoids the full-row re-render).
    private static bool TryUpdateInPlace(OrderLineResponse existing, OrderLineResponse fresh)
    {
        if (!FieldsUnchanged(existing, fresh)) return false;
        existing.OrderedAt = fresh.OrderedAt;
        return true;
    }

    [RelayCommand]
    private void SelectOrder(OrderLineResponse item)
    {
        if (item == null) return;
        // The tap gesture consumes the touch before native selection happens —
        // select here so the list shows the indicator.
        SelectedOrder = item;
        WeakReferenceMessenger.Default.Send(new ShowDetailMessage(new DetailRequest
        {
            Route = "orderDetail",
            // Wide mode hands the detail the part object directly.
            Properties = new() { ["Order"] = item },
            // Narrow mode (Shell push) carries just identifiers; the detail re-fetches.
            QueryParams = new()
            {
                ["itemId"] = item.Id.ToString(),
                ["source"] = item.Source,
            }
        }));
    }
}
