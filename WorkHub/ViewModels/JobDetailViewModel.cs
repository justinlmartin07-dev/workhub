using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using CommunityToolkit.Mvvm.Messaging;
using WorkHub.Messages;
using WorkHub.Models;
using WorkHub.Services;
using WorkHub.Views;

namespace WorkHub.ViewModels;

[QueryProperty(nameof(JobId), "id")]
public partial class JobDetailViewModel : BaseViewModel
{
    private readonly ApiService _apiService;
    private readonly PhotoService _photoService;
    private readonly ListCacheService _listCache;
    private readonly PhotoCacheService _photoCache;

    private string CacheKey => $"job-{JobId}";

    [ObservableProperty]
    private string? _jobId;

    [ObservableProperty]
    private JobResponse? _job;

    [ObservableProperty]
    private ObservableCollection<PhotoDisplayModel> _photos = new();

    [ObservableProperty]
    private ObservableCollection<JobItemResponse> _usedItems = new();

    [ObservableProperty]
    private ObservableCollection<JobItemResponse> _toOrderItems = new();

    [ObservableProperty]
    private string _newNoteText = string.Empty;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(IsEditingNote))]
    private JobNoteResponse? _editingNote;

    public bool IsEditingNote => EditingNote != null;

    /// <summary>Raised when a note is loaded into the compose box for editing, so the view can focus it.</summary>
    public event EventHandler? NoteEditRequested;

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

    public JobDetailViewModel(ApiService apiService, PhotoService photoService,
        ListCacheService listCache, PhotoCacheService photoCache)
    {
        _apiService = apiService;
        _photoService = photoService;
        _listCache = listCache;
        _photoCache = photoCache;
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

        // First load: render the last-known copy instantly, no spinner.
        if (Job == null)
        {
            var cached = await _listCache.LoadObjectAsync<JobResponse>(CacheKey);
            if (cached != null && Job == null)
            {
                ApplyJob(cached, urlsAreFresh: false);
                SetContent();
            }
        }

        // Refresh from the network; silent when something is already showing.
        await LoadAsync(async () =>
        {
            JobResponse? fresh;
            try
            {
                fresh = await _apiService.GetJobAsync(id);
            }
            catch (HttpRequestException ex) when (ex.StatusCode == System.Net.HttpStatusCode.NotFound)
            {
                // Deleted elsewhere — drop the cache and leave instead of
                // showing a ghost page.
                _listCache.Remove(CacheKey);
                await HandleJobGoneAsync();
                return;
            }
            if (fresh == null) return;

            // Presigned photo URLs differ on every response; only rebind when
            // something the user can see actually changed.
            if (Job == null || !JsonComparison.EqualIgnoringUrls(Job, fresh))
                ApplyJob(fresh, urlsAreFresh: true);
            else
                UpdatePhotos(fresh.Photos, urlsAreFresh: true);

            _ = _listCache.SaveObjectAsync(CacheKey, fresh);
        }, showLoading: Job == null);
    }

    private void ApplyJob(JobResponse job, bool urlsAreFresh)
    {
        Job = job;
        UsedItems.MergeInto(job.UsedItems ?? [], i => i.Id, ItemUnchanged);
        ToOrderItems.MergeInto(job.ToOrderItems ?? [], i => i.Id, ItemUnchanged);
        UpdatePhotos(job.Photos, urlsAreFresh);
    }

    private static bool ItemUnchanged(JobItemResponse a, JobItemResponse b) =>
        a.Name == b.Name
        && a.Description == b.Description
        && a.PartNumber == b.PartNumber
        && a.Quantity == b.Quantity
        && a.ListType == b.ListType
        && a.Source == b.Source;

    // Sync the photo wrappers to the latest response. Existing wrappers keep
    // their identity (and their already-loaded ImageSource) — only their
    // Photo reference is swapped for the new presigned URL.
    private void UpdatePhotos(List<PhotoResponse>? photos, bool urlsAreFresh)
    {
        var fresh = (photos ?? []).Select(p =>
        {
            var existing = Photos.FirstOrDefault(w => w.Id == p.Id);
            if (existing != null)
            {
                existing.UpdatePhoto(p);
                return existing;
            }
            return new PhotoDisplayModel(p);
        }).ToList();

        Photos.MergeInto(fresh, w => w.Id, ReferenceEquals);
        foreach (var wrapper in Photos)
            _ = wrapper.ResolveAsync(_photoCache, urlsAreFresh);
    }

    private async Task HandleJobGoneAsync()
    {
        if (Views.MainLayout.Current?.IsWideLayout == true)
        {
            Views.MainLayout.Current.ClearDetail();
            WeakReferenceMessenger.Default.Send(new DataChangedMessage("job"));
        }
        else
        {
            await Shell.Current.GoToAsync("..");
        }
    }

    [RelayCommand]
    private void Edit()
    {
        WeakReferenceMessenger.Default.Send(new ShowDetailMessage(new DetailRequest
        {
            Route = "jobEdit",
            Properties = new() { ["JobId"] = JobId! },
            QueryParams = new() { ["id"] = JobId! }
        }));
    }

    [RelayCommand]
    private async Task GoBackAsync() => await Shell.Current.GoToAsync("..");

    [RelayCommand]
    private async Task OpenAddressInMapsAsync()
    {
        var address = Job?.Address;
        if (string.IsNullOrWhiteSpace(address)) return;

        var encoded = Uri.EscapeDataString(address);
        try
        {
#if ANDROID
            await Launcher.OpenAsync($"geo:0,0?q={encoded}");
#else
            await Launcher.OpenAsync($"https://www.google.com/maps/search/?api=1&query={encoded}");
#endif
        }
        catch
        {
            await Shell.Current.DisplayAlert("Error", "Could not open Maps.", "OK");
        }
    }

    [RelayCommand]
    private async Task OpenAddressInEarthAsync()
    {
        var address = Job?.Address;
        if (string.IsNullOrWhiteSpace(address)) return;

        var encoded = Uri.EscapeDataString(address);
        try
        {
#if ANDROID
            await Launcher.OpenAsync($"com.google.earth:/search?q={encoded}");
#else
            await Launcher.OpenAsync($"https://earth.google.com/web/search/{encoded}");
#endif
        }
        catch
        {
            await Shell.Current.DisplayAlert("Error", "Could not open Google Earth.", "OK");
        }
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
            _listCache.Remove(CacheKey);
            if (Views.MainLayout.Current?.IsWideLayout == true)
            {
                Views.MainLayout.Current.ClearDetail();
                WeakReferenceMessenger.Default.Send(new DataChangedMessage("job"));
            }
            else
            {
                await Shell.Current.GoToAsync("..");
            }
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
            if (EditingNote != null)
            {
                await _apiService.UpdateJobNoteAsync(Job.Id, EditingNote.Id, new UpdateJobNoteRequest { Content = NewNoteText.Trim() });
                EditingNote = null;
            }
            else
            {
                await _apiService.CreateJobNoteAsync(Job.Id, new CreateJobNoteRequest { Content = NewNoteText.Trim() });
            }
            NewNoteText = string.Empty;
            await LoadJobAsync();
        }
        catch (Exception ex)
        {
            await Shell.Current.DisplayAlert("Error", ex.Message, "OK");
        }
    }

    [RelayCommand]
    private void EditNote(JobNoteResponse note)
    {
        EditingNote = note;
        NewNoteText = note.Content;
        NoteEditRequested?.Invoke(this, EventArgs.Empty);
    }

    [RelayCommand]
    private void CancelEditNote()
    {
        EditingNote = null;
        NewNoteText = string.Empty;
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
    private async Task ViewPhotosAsync(PhotoDisplayModel photo)
    {
        if (Job == null || Photos.Count == 0) return;
        var index = Photos.IndexOf(photo);
        if (index < 0) index = 0;
        await PhotoViewerLauncher.ShowAsync($"{Job.Title} Pictures", Photos, index);
    }

    [RelayCommand]
    private void ViewCustomer()
    {
        if (Job == null) return;
        var id = Job.CustomerId.ToString();
        WeakReferenceMessenger.Default.Send(new ShowDetailMessage(new DetailRequest
        {
            Route = "customerDetail",
            Properties = new() { ["CustomerId"] = id },
            QueryParams = new() { ["id"] = id },
            SwitchTabIndex = 0
        }));
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
