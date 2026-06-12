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

    // Full dataset; Items below is the (possibly search-filtered) view of it.
    private List<InventoryItemResponse> _allItems = new();

    [ObservableProperty]
    private ObservableCollection<InventoryItemResponse> _items = new();

    [ObservableProperty]
    private string _searchText = string.Empty;

    [ObservableProperty]
    private InventoryItemResponse? _selectedItem;

    public InventoryViewModel(ApiService apiService, ListCacheService listCache)
    {
        _apiService = apiService;
        _listCache = listCache;

        WeakReferenceMessenger.Default.Register<DataChangedMessage>(this, (r, m) =>
        {
            if (m.Value == "inventory")
                MainThread.BeginInvokeOnMainThread(() => LoadItemsCommand.Execute(null));
        });
    }

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
        }, showLoading: Items.Count == 0);
    }

    // Projects the master list through the current search filter into the bound
    // collection. rebuild swaps the collection wholesale (right for filter changes,
    // where most rows differ); otherwise rows are merged in place so only actual
    // changes re-render.
    private void PublishList(bool rebuild)
    {
        var query = SearchText.Trim();
        var visible = query.Length == 0
            ? _allItems
            : _allItems.Where(i => MatchesSearch(i, query)).ToList();

        if (rebuild || Items.Count == 0 || visible.Count == 0)
            Items = new ObservableCollection<InventoryItemResponse>(visible);
        else
            Items.MergeInto(visible, i => i.Id, RowUnchanged);

        if (Items.Count == 0) SetEmpty();
        else SetContent();
    }

    private static bool MatchesSearch(InventoryItemResponse i, string query) =>
        i.Name.Contains(query, StringComparison.OrdinalIgnoreCase)
        || (i.PartNumber?.Contains(query, StringComparison.OrdinalIgnoreCase) ?? false)
        || (i.Description?.Contains(query, StringComparison.OrdinalIgnoreCase) ?? false);

    private static bool RowUnchanged(InventoryItemResponse a, InventoryItemResponse b) =>
        a.Name == b.Name
        && a.Description == b.Description
        && a.PartNumber == b.PartNumber
        && a.UpdatedAt == b.UpdatedAt;

    // Search filters the in-memory list — instant, no debounce, no network.
    partial void OnSearchTextChanged(string value) => PublishList(rebuild: true);

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
