using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using WorkHub.Models;
using WorkHub.Services;

namespace WorkHub.ViewModels;

[QueryProperty(nameof(JobId), "id")]
public partial class JobDetailViewModel : BaseViewModel
{
    private readonly ApiService _apiService;
    private readonly PhotoService _photoService;

    [ObservableProperty]
    private string? _jobId;

    [ObservableProperty]
    private JobResponse? _job;

    [ObservableProperty]
    private ObservableCollection<JobItemResponse> _usedItems = new();

    [ObservableProperty]
    private ObservableCollection<JobItemResponse> _toOrderItems = new();

    [ObservableProperty]
    private string _newNoteText = string.Empty;

    [ObservableProperty]
    private int _locationPhotoCount;

    [ObservableProperty]
    private bool _isPartsPanelOpen;

    [ObservableProperty]
    private string _partsPanelTitle = "Add Parts";

    [ObservableProperty]
    private string _newItemSearchText = string.Empty;

    [ObservableProperty]
    private ObservableCollection<SelectableInventoryItem> _selectableInventory = new();

    [ObservableProperty]
    private string _newAdhocItemName = string.Empty;

    [ObservableProperty]
    private int _selectedCount;

    private List<InventoryItemResponse> _allInventory = new();
    private string _activeListType = string.Empty;

    public JobDetailViewModel(ApiService apiService, PhotoService photoService)
    {
        _apiService = apiService;
        _photoService = photoService;
    }

    partial void OnJobIdChanged(string? value)
    {
        if (Guid.TryParse(value, out _))
            LoadJobCommand.Execute(null);
    }

    [RelayCommand]
    public async Task LoadJobAsync()
    {
        if (!Guid.TryParse(JobId, out var id)) return;
        await LoadAsync(async () =>
        {
            Job = await _apiService.GetJobAsync(id);
            UsedItems = new ObservableCollection<JobItemResponse>(Job?.UsedItems ?? []);
            ToOrderItems = new ObservableCollection<JobItemResponse>(Job?.ToOrderItems ?? []);
        });
    }

    [RelayCommand]
    private async Task EditAsync()
    {
        await Shell.Current.GoToAsync($"jobEdit?id={JobId}");
    }

    [RelayCommand]
    private async Task DeleteAsync()
    {
        if (Job == null) return;
        bool confirm = await Shell.Current.DisplayAlert("Delete Job", $"Delete {Job.Title}?", "Delete", "Cancel");
        if (!confirm) return;
        try
        {
            await _apiService.DeleteJobAsync(Job.Id);
            await Shell.Current.GoToAsync("..");
        }
        catch (Exception ex)
        {
            await Shell.Current.DisplayAlert("Error", ex.Message, "OK");
        }
    }

    [RelayCommand]
    private async Task AddNoteAsync()
    {
        if (string.IsNullOrWhiteSpace(NewNoteText) || Job == null) return;
        try
        {
            await _apiService.CreateJobNoteAsync(Job.Id, new CreateJobNoteRequest { Content = NewNoteText.Trim() });
            NewNoteText = string.Empty;
            await LoadJobAsync();
        }
        catch (Exception ex)
        {
            await Shell.Current.DisplayAlert("Error", ex.Message, "OK");
        }
    }

    [RelayCommand]
    private async Task DeleteNoteAsync(JobNoteResponse note)
    {
        if (Job == null) return;
        bool confirm = await Shell.Current.DisplayAlert("Delete Note", "Delete this note?", "Delete", "Cancel");
        if (!confirm) return;
        await _apiService.DeleteJobNoteAsync(Job.Id, note.Id);
        await LoadJobAsync();
    }

    [RelayCommand]
    private async Task PickPhotoAsync()
    {
        if (Job == null) return;
        var photo = await _photoService.PickAndUploadJobPhotoAsync(Job.Id);
        if (photo != null) await LoadJobAsync();
    }

    [RelayCommand]
    private async Task TakePhotoAsync()
    {
        if (Job == null) return;
        var photo = await _photoService.CaptureAndUploadJobPhotoAsync(Job.Id);
        if (photo != null) await LoadJobAsync();
    }

    [RelayCommand]
    private async Task DeletePhotoAsync(PhotoResponse photo)
    {
        bool confirm = await Shell.Current.DisplayAlert("Delete Photo", "Delete this photo?", "Delete", "Cancel");
        if (!confirm) return;
        await _apiService.DeletePhotoAsync(photo.Id);
        await LoadJobAsync();
    }

    [RelayCommand]
    private async Task ViewPhotosAsync(PhotoResponse photo)
    {
        if (Job?.Photos == null) return;
        var index = Job.Photos.IndexOf(photo);
        await Shell.Current.GoToAsync($"photoViewer?entityType=job&entityId={JobId}&startIndex={index}");
    }

    [RelayCommand]
    private async Task ViewCustomerAsync()
    {
        if (Job == null) return;
        await Shell.Current.GoToAsync($"customerDetail?id={Job.CustomerId}");
    }

    partial void OnNewItemSearchTextChanged(string value) => FilterInventory();

    private void FilterInventory()
    {
        var search = (NewItemSearchText ?? string.Empty).ToLower();
        var filtered = string.IsNullOrWhiteSpace(search)
            ? _allInventory
            : _allInventory.Where(i =>
                i.Name.ToLower().Contains(search) ||
                (i.PartNumber?.ToLower().Contains(search) ?? false)).ToList();

        SelectableInventory = new ObservableCollection<SelectableInventoryItem>(
            filtered.Select(i =>
            {
                var si = new SelectableInventoryItem(i);
                si.PropertyChanged += (s, e) =>
                {
                    if (e.PropertyName == nameof(SelectableInventoryItem.IsSelected))
                        SelectedCount = SelectableInventory.Count(x => x.IsSelected);
                };
                return si;
            }));
    }

    [RelayCommand]
    private async Task OpenPartsPanelAsync(string listType)
    {
        _activeListType = listType;
        PartsPanelTitle = listType == "used" ? "Add Parts Used" : "Add Parts To Order";
        NewItemSearchText = string.Empty;
        NewAdhocItemName = string.Empty;
        SelectedCount = 0;

        try
        {
            var result = await _apiService.GetInventoryAsync(pageSize: 200);
            _allInventory = result.Items.ToList();
            FilterInventory();
        }
        catch { }

        IsPartsPanelOpen = true;
    }

    [RelayCommand]
    private void ClosePartsPanel()
    {
        IsPartsPanelOpen = false;
    }

    [RelayCommand]
    private async Task ConfirmAddPartsAsync()
    {
        if (Job == null) return;
        try
        {
            var selected = SelectableInventory.Where(i => i.IsSelected).ToList();
            foreach (var item in selected)
            {
                await _apiService.CreateJobItemAsync(Job.Id, new CreateJobInventoryRequest
                {
                    InventoryItemId = item.Item.Id,
                    Quantity = 1,
                    ListType = _activeListType
                });
            }

            if (!string.IsNullOrWhiteSpace(NewAdhocItemName))
            {
                await _apiService.CreateJobAdhocItemAsync(Job.Id, new CreateJobAdhocItemRequest
                {
                    Name = NewAdhocItemName.Trim(),
                    Quantity = 1,
                    ListType = _activeListType
                });
            }

            IsPartsPanelOpen = false;
            await LoadJobAsync();
        }
        catch (Exception ex)
        {
            await Shell.Current.DisplayAlert("Error", ex.Message, "OK");
        }
    }

    [RelayCommand]
    private void UpdateQuantity(QuantityUpdateRequest req)
    {
        if (Job == null) return;
        if (req.Quantity < 1 || req.Quantity == req.Item.Quantity) return;
        req.Item.Quantity = req.Quantity;
        SaveQuantityInBackground(req.Item, req.Quantity);
    }

    public void SaveQuantityInBackground(JobItemResponse item, int newQuantity)
    {
        if (Job == null) return;
        _ = SaveQuantityAsync(item, newQuantity);
    }

    private async Task SaveQuantityAsync(JobItemResponse item, int newQuantity)
    {
        try
        {
            if (item.Source == "library")
                await _apiService.UpdateJobItemAsync(Job!.Id, item.Id, new UpdateJobInventoryRequest { Quantity = newQuantity });
            else
                await _apiService.UpdateJobAdhocItemAsync(Job!.Id, item.Id, new UpdateJobAdhocItemRequest { Quantity = newQuantity });
        }
        catch (Exception ex)
        {
            await Shell.Current.DisplayAlert("Error", ex.Message, "OK");
        }
    }

    [RelayCommand]
    private async Task DeleteItemAsync(JobItemResponse item)
    {
        if (Job == null) return;
        bool confirm = await Shell.Current.DisplayAlert("Remove Item", $"Remove {item.Name}?", "Remove", "Cancel");
        if (!confirm) return;
        if (item.Source == "library")
            await _apiService.DeleteJobItemAsync(Job.Id, item.Id);
        else
            await _apiService.DeleteJobAdhocItemAsync(Job.Id, item.Id);
        await LoadJobAsync();
    }
}

public partial class SelectableInventoryItem : ObservableObject
{
    public InventoryItemResponse Item { get; }

    [ObservableProperty]
    private bool _isSelected;

    public string Name => Item.Name;
    public string? PartNumber => Item.PartNumber;
    public string? Description => Item.Description;

    public SelectableInventoryItem(InventoryItemResponse item)
    {
        Item = item;
    }
}

public record QuantityUpdateRequest(JobItemResponse Item, int Quantity);
