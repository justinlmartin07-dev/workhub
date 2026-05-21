using System.Net.Http.Json;
using System.Text.Json.Nodes;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using CommunityToolkit.Mvvm.Messaging;
using WorkHub.Messages;
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
    [NotifyPropertyChangedFor(nameof(HasChanges))]
    private string _name = string.Empty;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(HasChanges))]
    private string _description = string.Empty;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(HasChanges))]
    private string _partNumber = string.Empty;

    [ObservableProperty]
    private bool _isNew = true;

    [ObservableProperty]
    private string _pageTitle = "New Item";

    private string _originalName = string.Empty;
    private string _originalDescription = string.Empty;
    private string _originalPartNumber = string.Empty;

    public bool HasChanges =>
        !string.Equals(Name ?? string.Empty, _originalName, StringComparison.Ordinal)
        || !string.Equals(Description ?? string.Empty, _originalDescription, StringComparison.Ordinal)
        || !string.Equals(PartNumber ?? string.Empty, _originalPartNumber, StringComparison.Ordinal);

    public InventoryItemDetailViewModel(ApiService apiService)
    {
        _apiService = apiService;
    }

    partial void OnItemIdChanged(string? value)
    {
        if (Guid.TryParse(value, out _))
        {
            IsNew = false;
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
                SnapshotOriginal();
            }
        });
    }

    private void SnapshotOriginal()
    {
        _originalName = Name ?? string.Empty;
        _originalDescription = Description ?? string.Empty;
        _originalPartNumber = PartNumber ?? string.Empty;
        OnPropertyChanged(nameof(HasChanges));
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
                NavigateBack();
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
                SnapshotOriginal();
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
            var response = await _apiService.DeleteInventoryItemAsync(Guid.Parse(ItemId!));
            if (response.StatusCode == System.Net.HttpStatusCode.Conflict)
            {
                var body = await response.Content.ReadFromJsonAsync<JsonObject>();
                var jobs = body?["details"]?["referencingJobs"]?.AsArray();
                var jobNames = jobs != null
                    ? string.Join(", ", jobs.Select(j => j?["title"]?.GetValue<string>() ?? "Unknown"))
                    : "unknown jobs";
                await Shell.Current.DisplayAlert("Cannot Delete", $"This item is referenced by: {jobNames}", "OK");
                return;
            }
            response.EnsureSuccessStatusCode();
            WeakReferenceMessenger.Default.Send(new DataChangedMessage("inventory"));
            NavigateBack();
        }
        catch (Exception ex)
        {
            await Shell.Current.DisplayAlert("Error", ex.Message, "OK");
        }
    }

    [RelayCommand]
    private void Cancel() => NavigateBack();

    private async void NavigateBack()
    {
        if (Views.MainLayout.Current?.IsWideLayout == true)
            Views.MainLayout.Current.ClearDetail();
        else
            await Shell.Current.GoToAsync("..");
    }
}
