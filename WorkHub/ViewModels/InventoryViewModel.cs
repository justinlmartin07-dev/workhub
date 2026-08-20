using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using CommunityToolkit.Mvvm.Messaging;
using WorkHub.Messages;
using WorkHub.Models;
using WorkHub.Services;

namespace WorkHub.ViewModels;

public partial class InventoryViewModel : BaseViewModel
{
    private const string CacheKey = "inventory";

    private readonly ApiService _apiService;
    private readonly ListCacheService _listCache;
    private readonly AuthService _authService;

    private const string UncategorizedLabel = "Uncategorized";

    // Full dataset; Rows below is the (possibly search-filtered) view of it —
    // a flat mix of InventoryGroupHeader and InventoryItemResponse rows. Flat on
    // purpose: grouped CollectionView crashes on Windows when groups change.
    private List<InventoryItemResponse> _allItems = new();

    // Remembered per category so a refresh doesn't reset what the user collapsed.
    private readonly Dictionary<string, bool> _expandedState = new(StringComparer.OrdinalIgnoreCase);

    [ObservableProperty]
    private ObservableCollection<object> _rows = new();

    [ObservableProperty]
    private string _searchText = string.Empty;

    [ObservableProperty]
    private InventoryItemResponse? _selectedItem;

    // Raised when the selected row lands at a new position after a refresh
    // (e.g. its category changed) so the page can scroll it into view.
    public event Action<object>? ScrollToRowRequested;

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

    public InventoryViewModel(ApiService apiService, ListCacheService listCache, AuthService authService)
    {
        _apiService = apiService;
        _listCache = listCache;
        _authService = authService;
        _userPhotoUrl = authService.CurrentUser?.ProfilePhotoUrl;

        WeakReferenceMessenger.Default.Register<DataChangedMessage>(this, (r, m) =>
        {
            if (m.Value == "inventory")
                MainThread.BeginInvokeOnMainThread(() => LoadItemsCommand.Execute(null));
            else if (m.Value == "user_photo")
                MainThread.BeginInvokeOnMainThread(() => UserPhotoUrl = _authService.CurrentUser?.ProfilePhotoUrl);
        });
    }

    protected override Task OnRefreshRequestedAsync() => LoadItemsAsync();

    [RelayCommand]
    public async Task LoadItemsAsync()
    {
        // First load: show the last-known data from disk instantly, then let the
        // network refresh below merge in whatever changed.
        if (_allItems.Count == 0 && !IsBusy)
        {
            var cached = await _listCache.LoadAsync<InventoryItemResponse>(CacheKey);
            if (cached is { Count: > 0 } && _allItems.Count == 0)
            {
                _allItems = cached;
                PublishList(rebuild: true);
            }
        }

        await LoadAsync(async () =>
        {
            _allItems = await _apiService.GetAllInventoryAsync();
            PublishList(rebuild: false);
            _ = _listCache.SaveAsync(CacheKey, _allItems);
        }, showLoading: Rows.Count == 0);
    }

    // Projects the master list through the current search filter into the bound
    // flat collection of header + item rows. rebuild swaps the collection wholesale;
    // otherwise rows are merged in place so only actual changes re-render. visible
    // may be pre-computed off the main thread by the search debounce path.
    private void PublishList(bool rebuild, IReadOnlyList<InventoryItemResponse>? visible = null, bool followSelection = true)
    {
        var query = SearchText.Trim();
        visible ??= query.Length == 0
            ? _allItems
            : _allItems.Where(i => MatchesSearch(i, query)).ToList();

        var fresh = BuildRows(visible, forceExpand: query.Length > 0);

        if (rebuild || Rows.Count == 0 || fresh.Count == 0)
        {
            Rows = new ObservableCollection<object>(fresh);
        }
        else
        {
            var selected = SelectedItem;
            var oldIndex = selected != null ? Rows.IndexOf(selected) : -1;

            // If the selected item just moved into a collapsed group (category
            // change), expand that group so its row has a visible spot to land in.
            if (followSelection && selected != null && oldIndex >= 0
                && !fresh.OfType<InventoryItemResponse>().Any(i => i.Id == selected.Id))
            {
                var freshItem = visible.FirstOrDefault(i => i.Id == selected.Id);
                if (freshItem != null)
                {
                    var group = string.IsNullOrWhiteSpace(freshItem.Category) ? UncategorizedLabel : freshItem.Category!;
                    _expandedState[group] = true;
                    fresh = BuildRows(visible, forceExpand: query.Length > 0);
                }
            }

            Rows.MergeInto(fresh, RowKey, RowEqual, TryUpdateRowInPlace);

            // Follow the selected row if the refresh relocated it.
            if (followSelection && selected != null && oldIndex >= 0)
            {
                var newIndex = Rows.IndexOf(selected);
                if (newIndex >= 0 && newIndex != oldIndex)
                {
                    // After row moves WinUI can leave the selection ring at the old
                    // position — re-assert selection so it follows the item.
                    SelectedItem = null;
                    SelectedItem = selected;
                    ScrollToRowRequested?.Invoke(selected);
                }
            }
        }

        if (Rows.Count == 0) SetEmpty();
        else SetContent();
    }

    // While searching, groups are forced open so matches are actually visible.
    private List<object> BuildRows(IReadOnlyList<InventoryItemResponse> items, bool forceExpand)
    {
        var rows = new List<object>();
        var groups = items
            .GroupBy(i => string.IsNullOrWhiteSpace(i.Category) ? UncategorizedLabel : i.Category!)
            .OrderBy(g => g.Key == UncategorizedLabel ? 1 : 0)
            .ThenBy(g => g.Key, StringComparer.OrdinalIgnoreCase);
        foreach (var group in groups)
        {
            var expanded = forceExpand || IsExpandedFor(group.Key);
            var groupItems = group.ToList();
            rows.Add(new InventoryGroupHeader(group.Key, groupItems.Count, expanded));
            if (expanded) rows.AddRange(groupItems);
        }
        return rows;
    }

    private static string RowKey(object row) => row switch
    {
        InventoryGroupHeader h => "h:" + h.Category,
        InventoryItemResponse i => "i:" + i.Id,
        _ => throw new InvalidOperationException($"Unexpected row type {row.GetType()}"),
    };

    private static bool RowEqual(object a, object b) => (a, b) switch
    {
        (InventoryGroupHeader x, InventoryGroupHeader y) => x.ItemCount == y.ItemCount && x.IsExpanded == y.IsExpanded,
        (InventoryItemResponse x, InventoryItemResponse y) => RowUnchanged(x, y),
        _ => false,
    };

    // Headers and items are observable — mutate the existing instance instead of
    // replacing it. A Replace notification makes the WinUI list re-render the row
    // (flicker) and drop its scroll position, e.g. after saving an edit.
    private static bool TryUpdateRowInPlace(object existing, object fresh)
    {
        switch (existing, fresh)
        {
            case (InventoryGroupHeader h, InventoryGroupHeader f):
                h.ItemCount = f.ItemCount;
                h.IsExpanded = f.IsExpanded;
                return true;
            case (InventoryItemResponse x, InventoryItemResponse y):
                x.Name = y.Name;
                x.Description = y.Description;
                x.PartNumber = y.PartNumber;
                x.Sku = y.Sku;
                x.Category = y.Category;
                x.UpdatedAt = y.UpdatedAt;
                return true;
            default:
                return false;
        }
    }

    private bool IsExpandedFor(string category) =>
        !_expandedState.TryGetValue(category, out var expanded) || expanded;

    [RelayCommand]
    private void ToggleGroup(InventoryGroupHeader header)
    {
        if (header == null) return;
        _expandedState[header.Category] = !header.IsExpanded;
        // Re-publish: the merge flips the header state and inserts/removes the
        // group's item rows in place. A deliberate toggle must not re-expand the
        // selected item's group or scroll back to the selected row.
        PublishList(rebuild: false, followSelection: false);
    }

    private static bool MatchesSearch(InventoryItemResponse i, string query) =>
        i.Name.Contains(query, StringComparison.OrdinalIgnoreCase)
        || (i.PartNumber?.Contains(query, StringComparison.OrdinalIgnoreCase) ?? false)
        || (i.Sku?.Contains(query, StringComparison.OrdinalIgnoreCase) ?? false)
        || (i.Description?.Contains(query, StringComparison.OrdinalIgnoreCase) ?? false)
        || (i.Category?.Contains(query, StringComparison.OrdinalIgnoreCase) ?? false);

    private static bool RowUnchanged(InventoryItemResponse a, InventoryItemResponse b) =>
        a.Name == b.Name
        && a.Description == b.Description
        && a.PartNumber == b.PartNumber
        && a.Sku == b.Sku
        && a.Category == b.Category
        && a.UpdatedAt == b.UpdatedAt;

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
        var snapshot = _allItems;
        var visible = await Task.Run(() =>
            query.Length == 0 ? snapshot : snapshot.Where(i => MatchesSearch(i, query)).ToList(), ct);
        if (ct.IsCancellationRequested) return;
        PublishList(rebuild: false, visible);
    }

    [RelayCommand]
    private void Search() => PublishList(rebuild: true);

    [RelayCommand]
    private void SelectItem(InventoryItemResponse item)
    {
        if (item == null) return;
        // The tap gesture consumes the touch before native selection happens —
        // select here so the list shows the indicator.
        SelectedItem = item;
        var id = item.Id.ToString();
        WeakReferenceMessenger.Default.Send(new ShowDetailMessage(new DetailRequest
        {
            Route = "inventoryDetail",
            Properties = new() { ["ItemId"] = id },
            QueryParams = new() { ["id"] = id }
        }));
    }

    [RelayCommand]
    private async Task AddItemAsync()
    {
        await Shell.Current.GoToAsync("inventoryDetail");
    }
}
