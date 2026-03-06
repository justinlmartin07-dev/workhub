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

    private int _currentPage = 1;
    private int _totalPages = 1;

    public InventoryViewModel(ApiService apiService)
    {
        _apiService = apiService;
    }

    [RelayCommand]
    public async Task LoadItemsAsync()
    {
        _currentPage = 1;
        await LoadAsync(async () =>
        {
            var result = await _apiService.GetInventoryAsync(SearchText, _currentPage);
            _totalPages = result.TotalPages;
            Items = new ObservableCollection<InventoryItemResponse>(result.Items);
            if (Items.Count == 0) SetEmpty();
            else SetContent();
        });
    }

    [RelayCommand]
    private async Task LoadMoreAsync()
    {
        if (IsBusy || _currentPage >= _totalPages) return;
        _currentPage++;
        await LoadAsync(async () =>
        {
            var result = await _apiService.GetInventoryAsync(SearchText, _currentPage);
            foreach (var item in result.Items)
                Items.Add(item);
        }, showLoading: false);
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
