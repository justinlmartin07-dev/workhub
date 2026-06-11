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

    public CustomersListViewModel(ApiService apiService, ListCacheService listCache)
    {
        _apiService = apiService;
        _listCache = listCache;

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
        });
    }

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
    // collection. rebuild swaps the collection wholesale (right for filter changes,
    // where most rows differ); otherwise rows are merged in place so only actual
    // changes re-render.
    private void PublishList(bool rebuild)
    {
        var query = SearchText.Trim();
        var visible = query.Length == 0
            ? _allCustomers
            : _allCustomers.Where(c => MatchesSearch(c, query)).ToList();

        if (rebuild || Customers.Count == 0 || visible.Count == 0)
        {
            Customers = new ObservableCollection<CustomerResponse>(visible);
        }
        else
        {
            var selectedId = SelectedCustomer?.Id;
            Customers.MergeInto(visible, c => c.Id, RowUnchanged);
            ReselectById(selectedId);
        }

        if (Customers.Count == 0) SetEmpty();
        else SetContent();
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

    // Search filters the in-memory list — instant, no debounce, no network.
    partial void OnSearchTextChanged(string value) => PublishList(rebuild: true);

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
