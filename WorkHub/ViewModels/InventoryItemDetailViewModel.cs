using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using WorkHub.Models;
using WorkHub.Services;

namespace WorkHub.ViewModels;

[QueryProperty(nameof(ItemId), "id")]
public partial class InventoryItemDetailViewModel : BaseViewModel
{
    private readonly ApiService _apiService;

    [ObservableProperty]
    private string? _itemId;

    [ObservableProperty]
    private string _name = string.Empty;

    [ObservableProperty]
    private string _description = string.Empty;

    [ObservableProperty]
    private string _partNumber = string.Empty;

    [ObservableProperty]
    private bool _isNew = true;

    [ObservableProperty]
    private string _pageTitle = "New Item";

    [ObservableProperty]
    private bool _isEditing = true;

    public InventoryItemDetailViewModel(ApiService apiService)
    {
        _apiService = apiService;
    }

    partial void OnItemIdChanged(string? value)
    {
        if (Guid.TryParse(value, out _))
        {
            IsNew = false;
            IsEditing = false;
            PageTitle = "Item Details";
            LoadItemCommand.Execute(null);
        }
    }

    [RelayCommand]
    private async Task LoadItemAsync()
    {
        if (!Guid.TryParse(ItemId, out var id)) return;
        await LoadAsync(async () =>
        {
            var item = await _apiService.GetInventoryItemAsync(id);
            if (item != null)
            {
                Name = item.Name;
                Description = item.Description ?? string.Empty;
                PartNumber = item.PartNumber ?? string.Empty;
            }
        });
    }

    [RelayCommand]
    private void ToggleEdit()
    {
        IsEditing = !IsEditing;
        PageTitle = IsEditing ? "Edit Item" : "Item Details";
    }

    [RelayCommand]
    private async Task SaveAsync()
    {
        if (string.IsNullOrWhiteSpace(Name))
        {
            ErrorMessage = "Name is required";
            HasError = true;
            return;
        }

        await LoadAsync(async () =>
        {
            if (IsNew)
            {
                var request = new CreateInventoryItemRequest
                {
                    Name = Name.Trim(),
                    Description = string.IsNullOrWhiteSpace(Description) ? null : Description.Trim(),
                    PartNumber = string.IsNullOrWhiteSpace(PartNumber) ? null : PartNumber.Trim()
                };
                await _apiService.CreateInventoryItemAsync(request);
                await Shell.Current.GoToAsync("..");
            }
            else
            {
                var request = new UpdateInventoryItemRequest
                {
                    Name = Name.Trim(),
                    Description = string.IsNullOrWhiteSpace(Description) ? null : Description.Trim(),
                    PartNumber = string.IsNullOrWhiteSpace(PartNumber) ? null : PartNumber.Trim()
                };
                await _apiService.UpdateInventoryItemAsync(Guid.Parse(ItemId!), request);
                IsEditing = false;
                PageTitle = "Item Details";
            }
        });
    }

    [RelayCommand]
    private async Task DeleteAsync()
    {
        if (IsNew) return;
        bool confirm = await Shell.Current.DisplayAlert("Delete Item", $"Delete {Name}?", "Delete", "Cancel");
        if (!confirm) return;
        try
        {
            await _apiService.DeleteInventoryItemAsync(Guid.Parse(ItemId!));
            await Shell.Current.GoToAsync("..");
        }
        catch (Exception ex)
        {
            await Shell.Current.DisplayAlert("Error", ex.Message, "OK");
        }
    }

    [RelayCommand]
    private async Task CancelAsync()
    {
        if (!IsNew && IsEditing)
        {
            IsEditing = false;
            PageTitle = "Item Details";
            await LoadItemAsync();
        }
        else
        {
            await Shell.Current.GoToAsync("..");
        }
    }
}
