using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using CommunityToolkit.Mvvm.Messaging;
using WorkHub.Messages;
using WorkHub.Models;
using WorkHub.Services;

namespace WorkHub.ViewModels;

public partial class OrderDetailViewModel : BaseViewModel, IQueryAttributable
{
    private readonly ApiService _apiService;

    [ObservableProperty]
    private OrderLineResponse? _order;

    // Drives the transient "Copied" label next to the SKU copy button.
    // Toolkit Toast is NOT an option here — it crashes unpackaged Windows apps
    // (needs package identity for AppNotification).
    [ObservableProperty]
    private bool _skuCopied;

    private int _skuCopiedVersion;

    public OrderDetailViewModel(ApiService apiService) => _apiService = apiService;

    [RelayCommand]
    private void Cancel() => NavigateBack();

    [RelayCommand]
    private async Task CopySkuAsync()
    {
        if (string.IsNullOrWhiteSpace(Order?.Sku)) return;
        try
        {
            await Clipboard.Default.SetTextAsync(Order.Sku);
        }
        catch
        {
            return; // no feedback shown; copying is best-effort
        }

        // Show "Copied" briefly; the version counter keeps rapid re-taps from
        // hiding the label early.
        var version = ++_skuCopiedVersion;
        SkuCopied = true;
        await Task.Delay(1500);
        if (version == _skuCopiedVersion)
            SkuCopied = false;
    }

    private async void NavigateBack()
    {
        if (Views.MainLayout.Current?.IsWideLayout == true)
            Views.MainLayout.Current.ClearDetail();
        else
            await Shell.Current.GoToAsync("..");
    }

    // Narrow mode (Shell push) carries only id + source; re-fetch the part to display it.
    // Wide mode sets Order directly via reflection, so this is a no-op there.
    public async void ApplyQueryAttributes(IDictionary<string, object> query)
    {
        if (Order != null) return;
        if (!query.TryGetValue("itemId", out var rawId) || !Guid.TryParse(rawId?.ToString(), out var id))
            return;
        var source = query.TryGetValue("source", out var s) ? s?.ToString() ?? string.Empty : string.Empty;

        await LoadAsync(async () =>
        {
            var all = await _apiService.GetOrdersAsync();
            Order = all.FirstOrDefault(o => o.Id == id && o.Source == source);
            if (Order == null) SetEmpty();
            else SetContent();
        });
    }

    // Persists the ordered state, optimistically updating the part first.
    public async Task SetOrderedAsync(bool ordered)
    {
        if (Order == null || Order.IsOrdered == ordered) return;

        var previous = Order.OrderedAt;
        Order.OrderedAt = ordered ? DateTime.Now : null;

        try
        {
            await _apiService.SetJobItemOrderedAsync(Order.JobId, Order.Id, Order.Source, ordered);
            // Update only this row on the dashboard — no full network reload.
            WeakReferenceMessenger.Default.Send(new OrderOrderedChangedMessage(new OrderOrderedChange
            {
                ItemId = Order.Id,
                Source = Order.Source,
                OrderedAt = Order.OrderedAt,
            }));
        }
        catch
        {
            Order.OrderedAt = previous; // revert on failure
        }
    }
}
