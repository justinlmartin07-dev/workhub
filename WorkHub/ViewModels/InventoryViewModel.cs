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
    private readonly ApiService _apiService;

    [ObservableProperty]
    private ObservableCollection<InventoryItemResponse> _items = new();

    [ObservableProperty]
    private string _searchText = string.Empty;

    public InventoryViewModel(ApiService apiService)
    {
        _apiService = apiService;
    }

    [RelayCommand]
    public async Task LoadItemsAsync()
    {
        await LoadAsync(async () =>
        {
            var all = new List<InventoryItemResponse>();
            var page = 1;
            int totalPages;
            do
            {
                var result = await _apiService.GetInventoryAsync(SearchText, page);
                totalPages = result.TotalPages;
                all.AddRange(result.Items);
                page++;
            } while (page <= totalPages);

            Items = new ObservableCollection<InventoryItemResponse>(all);
            if (Items.Count == 0) SetEmpty();
            else SetContent();
        });
    }

    [RelayCommand]
    private async Task SearchAsync()
    {
        await LoadItemsAsync();
    }

    [RelayCommand]
    private void SelectItem(InventoryItemResponse item)
    {
        if (item == null) return;
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
