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
public partial class JobDetailViewModel : BaseViewModel, IReusableDetail
{
    private readonly ApiService _apiService;
    private readonly PhotoService _photoService;
    private readonly ListCacheService _listCache;
    private readonly PhotoCacheService _photoCache;
    private readonly PrintTemplateService _printTemplates;

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
    private string _uploadStatus = string.Empty;

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
        ListCacheService listCache, PhotoCacheService photoCache, PrintTemplateService printTemplates)
    {
        _apiService = apiService;
        _photoService = photoService;
        _listCache = listCache;
        _photoCache = photoCache;
        _printTemplates = printTemplates;
    }

    // Raised just before the reused view resets or reloads, while the outgoing
    // job's items are still bound — the page commits any in-progress quantity
    // edit here, because switching jobs doesn't unfocus the Entry on Windows.
    public event EventHandler? Reloading;

    partial void OnJobIdChanged(string? value)
    {
        if (!Guid.TryParse(value, out _)) return;
        Reloading?.Invoke(this, EventArgs.Empty);
        ResetForNewJob();
        LoadJobCommand.Execute(null);
    }

    // Same item shown again on the reused view — just refresh silently.
    public void RefreshOnReuse()
    {
        Reloading?.Invoke(this, EventArgs.Empty);
        LoadJobCommand.Execute(null);
    }

    // The detail view is cached and reused across items — wipe the previous
    // job's state so the new one starts from its own cache (or a spinner),
    // never from stale content or a leftover note draft.
    private void ResetForNewJob()
    {
        Job = null;
        Photos.Clear();
        UsedItems.Clear();
        ToOrderItems.Clear();
        NewNoteText = string.Empty;
        EditingNote = null;
        IsPartsPanelOpen = false;
        HasContent = false;
        HasError = false;
        IsEmpty = false;
    }

    private bool IsCurrent(Guid id) => Guid.TryParse(JobId, out var current) && current == id;

    [RelayCommand]
    public async Task LoadJobAsync()
    {
        if (!Guid.TryParse(JobId, out var id)) return;
        var cacheKey = $"job-{id}";

        // First load: render the last-known copy instantly, no spinner.
        if (Job == null)
        {
            var cached = await _listCache.LoadObjectAsync<JobResponse>(cacheKey);
            if (cached != null && Job == null && IsCurrent(id))
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
                _listCache.Remove(cacheKey);
                if (IsCurrent(id))
                    await HandleJobGoneAsync();
                return;
            }
            if (fresh == null) return;

            _ = _listCache.SaveObjectAsync(cacheKey, fresh);

            // The user may have moved to a different item mid-flight — never
            // apply a stale response over the new item's content.
            if (!IsCurrent(id)) return;

            // Presigned photo URLs differ on every response; only rebind when
            // something the user can see actually changed.
            if (Job == null || !JsonComparison.EqualIgnoringUrls(Job, fresh))
                ApplyJob(fresh, urlsAreFresh: true);
            else
                UpdatePhotos(fresh.Photos, urlsAreFresh: true);
        }, showLoading: Job == null);

        // If the id changed while a load was in flight, the IsBusy gate
        // swallowed the re-trigger — load again for the current id.
        if (!IsCurrent(id))
            await LoadJobAsync();
    }

    public bool CanComplete => Job?.Status is "New" or "In Progress";
    public bool CanBill     => Job?.Status is "Complete";
    public bool CanReopen   => Job?.Status is "Complete" or "Billed" or "Cancelled" or "On Hold";
    public bool CanHold     => Job?.Status is "New" or "In Progress";
    public bool CanCancel   => Job?.Status is "New" or "In Progress" or "On Hold";

    private void ApplyJob(JobResponse job, bool urlsAreFresh)
    {
        Job = job;
        OnPropertyChanged(nameof(CanComplete));
        OnPropertyChanged(nameof(CanBill));
        OnPropertyChanged(nameof(CanReopen));
        OnPropertyChanged(nameof(CanHold));
        OnPropertyChanged(nameof(CanCancel));
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
        && a.Source == b.Source
        && a.Cost == b.Cost
        && a.Price == b.Price;

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
    private async Task PrintAsync()
    {
        if (Job == null) return;

        // Pull the customer so the printout carries company contact details;
        // the summary still prints without them if the fetch fails.
        CustomerResponse? customer = null;
        try { customer = await _apiService.GetCustomerAsync(Job.CustomerId); }
        catch { }

        var template = await _printTemplates.GetJobTemplateAsync();
        var html = PrintSummaryBuilder.BuildJobSummary(template, Job, customer);
        await Shell.Current.Navigation.PushModalAsync(
            new PrintPreviewPage(html, $"{Job.Title} — Job Summary"));
    }

    [RelayCommand]
    private void CallPhone(string? phoneNumber)
    {
        if (!string.IsNullOrEmpty(phoneNumber))
        {
            try { PhoneDialer.Open(phoneNumber); }
            catch { }
        }
    }

    [RelayCommand]
    private async Task SendEmailAsync(string? email)
    {
        if (!string.IsNullOrEmpty(email))
        {
            try { await Launcher.OpenAsync($"mailto:{email}"); }
            catch { }
        }
    }

    // Releasing the long-press that opened the options popup still registers
    // as a tap (the tap recognizer doesn't disqualify long holds), which would
    // open Maps over the popup — swallow taps while the popup is up.
    private bool _addressOptionsOpen;

    [RelayCommand]
    private async Task OpenAddressInMapsAsync()
    {
        if (_addressOptionsOpen) return;
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
    private async Task ShowAddressOptionsAsync()
    {
        var address = Job?.Address;
        if (string.IsNullOrWhiteSpace(address)) return;

        bool openEarth;
        _addressOptionsOpen = true;
        try
        {
            openEarth = await Views.AddressOptionsPopup.ShowAsync(address);
        }
        finally
        {
            _addressOptionsOpen = false;
        }
        if (openEarth)
            await OpenAddressInEarthAsync();
    }

    [RelayCommand]
    private async Task OpenAddressInEarthAsync()
    {
        var address = Job?.Address;
        if (string.IsNullOrWhiteSpace(address)) return;

        var encoded = Uri.EscapeDataString(address);
        try
        {
            // The Earth app claims earth.google.com app links on Android, so this
            // opens the app when installed and the browser otherwise. (Earth's
            // custom URI scheme is unreliable and blocked by package visibility.)
            await Launcher.OpenAsync($"https://earth.google.com/web/search/{encoded}");
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
    private async Task MarkAsCompleteAsync()
    {
        if (Job == null) return;
        var confirmed = await Shell.Current.DisplayAlert("Complete Job", "Mark this job as complete?", "Mark Complete", "Cancel");
        if (!confirmed) return;
        await _apiService.UpdateJobAsync(Job.Id, new UpdateJobRequest { Status = "Complete" });
        await LoadJobAsync();
    }

    [RelayCommand]
    private async Task MarkAsBilledAsync()
    {
        if (Job == null) return;
        var confirmed = await Shell.Current.DisplayAlert("Bill Job", "Mark this job as billed?", "Mark Billed", "Cancel");
        if (!confirmed) return;
        await _apiService.UpdateJobAsync(Job.Id, new UpdateJobRequest { Status = "Billed" });
        await LoadJobAsync();
    }

    [RelayCommand]
    private async Task ReopenJobAsync()
    {
        if (Job == null) return;
        await _apiService.UpdateJobAsync(Job.Id, new UpdateJobRequest { Status = "In Progress" });
        await LoadJobAsync();
    }

    [RelayCommand]
    private async Task PutOnHoldAsync()
    {
        if (Job == null) return;
        await _apiService.UpdateJobAsync(Job.Id, new UpdateJobRequest { Status = "On Hold" });
        await LoadJobAsync();
    }

    [RelayCommand]
    private async Task CancelJobAsync()
    {
        if (Job == null) return;
        var confirmed = await Shell.Current.DisplayAlert("Cancel Job", "Are you sure you want to cancel this job?", "Cancel Job", "Keep");
        if (!confirmed) return;
        await _apiService.UpdateJobAsync(Job.Id, new UpdateJobRequest { Status = "Cancelled" });
        await LoadJobAsync();
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
        if (!await EnsureJobReopenedAsync()) return;
        var uploaded = await _photoService.PickAndUploadMultipleJobPhotosAsync(Job.Id, (current, total) =>
            UploadStatus = total > 1 ? $"Uploading {current} of {total}..." : "Uploading...");
        UploadStatus = string.Empty;
        if (uploaded.Count > 0) await LoadJobAsync();
    }

    [RelayCommand]
    private async Task TakePhotoAsync()
    {
        if (Job == null) return;
        if (!await EnsureJobReopenedAsync()) return;
        var photo = await _photoService.CaptureAndUploadJobPhotoAsync(Job.Id);
        if (photo != null) await LoadJobAsync();
    }

    private async Task<bool> EnsureJobReopenedAsync()
    {
        if (Job?.Status is not ("Complete" or "Billed")) return true;
        var reopen = await Shell.Current.DisplayAlert($"Job {Job.Status}",
            $"This job is marked {Job.Status.ToLowerInvariant()}. Reopen it to add items?", "Reopen", "Cancel");
        if (!reopen) return false;
        await _apiService.UpdateJobAsync(Job.Id, new UpdateJobRequest { Status = "In Progress" });
        return true;
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

    private CancellationTokenSource? _searchCts;

    partial void OnNewItemSearchTextChanged(string value)
    {
        _searchCts?.Cancel();
        _searchCts = new CancellationTokenSource();
        _ = DebounceSearchAsync(_searchCts.Token);
    }

    private async Task DebounceSearchAsync(CancellationToken ct)
    {
        try { await Task.Delay(200, ct); }
        catch (OperationCanceledException) { return; }
        FilterInventory();
    }

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
        if (!await EnsureJobReopenedAsync()) return;

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
