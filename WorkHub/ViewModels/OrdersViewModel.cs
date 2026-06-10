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
    private readonly ApiService _apiService;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(OutstandingCount))]
    [NotifyPropertyChangedFor(nameof(OrderedCount))]
    private ObservableCollection<OrderLineResponse> _orders = new();

    public int OutstandingCount => Orders.Count(o => !o.IsOrdered);
    public int OrderedCount => Orders.Count(o => o.IsOrdered);

    public OrdersViewModel(ApiService apiService)
    {
        _apiService = apiService;

        // Adding/removing to-order parts (or job changes) needs a fresh list.
        WeakReferenceMessenger.Default.Register<DataChangedMessage>(this, (r, m) =>
        {
            if (m.Value is "orders" or "job")
                MainThread.BeginInvokeOnMainThread(() => LoadOrdersCommand.Execute(null));
        });

        // Marking a single part ordered updates just that row — no network reload.
        WeakReferenceMessenger.Default.Register<OrderOrderedChangedMessage>(this, (r, m) =>
        {
            MainThread.BeginInvokeOnMainThread(() => ApplyOrderedChange(m.Value));
        });
    }

    private void ApplyOrderedChange(OrderOrderedChange change)
    {
        var item = Orders.FirstOrDefault(o => o.Id == change.ItemId && o.Source == change.Source);
        if (item != null)
            item.OrderedAt = change.OrderedAt;

        OnPropertyChanged(nameof(OutstandingCount));
        OnPropertyChanged(nameof(OrderedCount));
    }

    [RelayCommand]
    public async Task LoadOrdersAsync()
    {
        await LoadAsync(async () =>
        {
            var orders = await _apiService.GetOrdersAsync();

            if (Orders.Count == 0)
            {
                Orders = new ObservableCollection<OrderLineResponse>(orders);
            }
            else
            {
                Orders.MergeInto(orders, o => (o.Id, o.Source), RowUnchanged, TryUpdateInPlace);
                OnPropertyChanged(nameof(OutstandingCount));
                OnPropertyChanged(nameof(OrderedCount));
            }

            if (Orders.Count == 0) SetEmpty();
            else SetContent();
        }, showLoading: Orders.Count == 0);
    }

    private static bool RowUnchanged(OrderLineResponse a, OrderLineResponse b) =>
        FieldsUnchanged(a, b) && a.OrderedAt == b.OrderedAt;

    private static bool FieldsUnchanged(OrderLineResponse a, OrderLineResponse b) =>
        a.Name == b.Name
        && a.Description == b.Description
        && a.PartNumber == b.PartNumber
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
