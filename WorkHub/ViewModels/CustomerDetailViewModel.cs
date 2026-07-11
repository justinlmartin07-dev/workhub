using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using CommunityToolkit.Mvvm.Messaging;
using WorkHub.Messages;
using WorkHub.Models;
using WorkHub.Services;
using WorkHub.Views;

namespace WorkHub.ViewModels;

// Cached detail snapshot: the customer plus the location-photo count, so both
// render instantly on revisit.
public class CustomerDetailCacheEntry
{
    public CustomerResponse Customer { get; set; } = null!;
    public int LocationPhotoCount { get; set; }
}

[QueryProperty(nameof(CustomerId), "id")]
public partial class CustomerDetailViewModel : BaseViewModel, IReusableDetail
{
    private readonly ApiService _apiService;
    private readonly PhotoService _photoService;
    private readonly ListCacheService _listCache;
    private readonly PhotoCacheService _photoCache;

    private string CacheKey => $"customer-{CustomerId}";

    [ObservableProperty]
    private string? _customerId;

    [ObservableProperty]
    private CustomerResponse? _customer;

    [ObservableProperty]
    private ObservableCollection<PhotoDisplayModel> _photos = new();

    [ObservableProperty]
    private int _locationPhotoCount;

    [ObservableProperty]
    private string _uploadStatus = string.Empty;

    public List<CustomerContactResponse> PhoneContacts =>
        Customer?.Contacts?.Where(c => c.Type == "phone").ToList() ?? [];

    public List<CustomerContactResponse> EmailContacts =>
        Customer?.Contacts?.Where(c => c.Type == "email").ToList() ?? [];

    public List<ContactPersonResponse> Persons => Customer?.Persons ?? [];
    public bool HasPersons => Persons.Count > 0;

    public CustomerDetailViewModel(ApiService apiService, PhotoService photoService,
        ListCacheService listCache, PhotoCacheService photoCache)
    {
        _apiService = apiService;
        _photoService = photoService;
        _listCache = listCache;
        _photoCache = photoCache;
    }

    partial void OnCustomerChanged(CustomerResponse? value)
    {
        OnPropertyChanged(nameof(PhoneContacts));
        OnPropertyChanged(nameof(EmailContacts));
        OnPropertyChanged(nameof(Persons));
        OnPropertyChanged(nameof(HasPersons));
    }

    partial void OnCustomerIdChanged(string? value)
    {
        if (!Guid.TryParse(value, out _)) return;
        ResetForNewCustomer();
        LoadCustomerCommand.Execute(null);
    }

    // Same item shown again on the reused view — just refresh silently.
    public void RefreshOnReuse() => LoadCustomerCommand.Execute(null);

    // The detail view is cached and reused across items — wipe the previous
    // customer's state so the new one starts from its own cache (or a spinner).
    private void ResetForNewCustomer()
    {
        Customer = null;
        Photos.Clear();
        LocationPhotoCount = 0;
        HasContent = false;
        HasError = false;
        IsEmpty = false;
    }

    private bool IsCurrent(Guid id) => Guid.TryParse(CustomerId, out var current) && current == id;

    [RelayCommand]
    public async Task LoadCustomerAsync()
    {
        if (!Guid.TryParse(CustomerId, out var id)) return;
        var cacheKey = $"customer-{id}";

        // First load: render the last-known copy instantly, no spinner.
        if (Customer == null)
        {
            var cached = await _listCache.LoadObjectAsync<CustomerDetailCacheEntry>(cacheKey);
            if (cached?.Customer != null && Customer == null && IsCurrent(id))
            {
                ApplyCustomer(cached.Customer, urlsAreFresh: false);
                LocationPhotoCount = cached.LocationPhotoCount;
                SetContent();
            }
        }

        // Refresh from the network; silent when something is already showing.
        await LoadAsync(async () =>
        {
            // Kick off the location-photo count alongside the customer fetch
            // using the best-known address, instead of serially afterwards.
            var knownAddress = Customer?.Address;
            var countTask = knownAddress != null
                ? _apiService.GetLocationPhotoCountAsync(knownAddress, excludeCustomerId: id)
                : null;

            CustomerResponse? fresh;
            try
            {
                fresh = await _apiService.GetCustomerAsync(id);
            }
            catch (HttpRequestException ex) when (ex.StatusCode == System.Net.HttpStatusCode.NotFound)
            {
                ObserveAbandoned(countTask);
                _listCache.Remove(cacheKey);
                if (IsCurrent(id))
                    await HandleCustomerGoneAsync();
                return;
            }
            if (fresh == null || !IsCurrent(id))
            {
                // The user may have moved to a different item mid-flight —
                // never apply a stale response over the new item's content.
                ObserveAbandoned(countTask);
                return;
            }

            // Render the customer before waiting on the (decorative) count.
            // Presigned photo URLs differ on every response; only rebind when
            // something the user can see actually changed.
            if (Customer == null || !JsonComparison.EqualIgnoringUrls(Customer, fresh))
                ApplyCustomer(fresh, urlsAreFresh: true);
            else
                UpdatePhotos(fresh.Photos, urlsAreFresh: true);

            var count = LocationPhotoCount;
            try
            {
                if (countTask != null && fresh.Address == knownAddress)
                    count = await countTask;
                else if (fresh.Address != null)
                {
                    ObserveAbandoned(countTask);
                    count = await _apiService.GetLocationPhotoCountAsync(fresh.Address, excludeCustomerId: id);
                }
                else
                    count = 0;
            }
            catch
            {
                // Count is decorative — keep the last-known value on failure.
            }
            if (!IsCurrent(id)) return;
            LocationPhotoCount = count;

            _ = _listCache.SaveObjectAsync(cacheKey,
                new CustomerDetailCacheEntry { Customer = fresh, LocationPhotoCount = count });
        }, showLoading: Customer == null);

        // If the id changed while a load was in flight, the IsBusy gate
        // swallowed the re-trigger — load again for the current id.
        if (!IsCurrent(id))
            await LoadCustomerAsync();
    }

    // Prevent an unobserved-exception fault when a started count task is dropped.
    private static void ObserveAbandoned(Task? task)
    {
        if (task != null)
            _ = task.ContinueWith(t => _ = t.Exception, TaskContinuationOptions.OnlyOnFaulted);
    }

    private void ApplyCustomer(CustomerResponse customer, bool urlsAreFresh)
    {
        Customer = customer;
        UpdatePhotos(customer.Photos, urlsAreFresh);
    }

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

    private async Task HandleCustomerGoneAsync()
    {
        if (Views.MainLayout.Current?.IsWideLayout == true)
        {
            Views.MainLayout.Current.ClearDetail();
            WeakReferenceMessenger.Default.Send(new DataChangedMessage("customer"));
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
            Route = "customerEdit",
            Properties = new() { ["CustomerId"] = CustomerId! },
            QueryParams = new() { ["id"] = CustomerId! }
        }));
    }

    [RelayCommand]
    private async Task GoBackAsync() => await Shell.Current.GoToAsync("..");

    [RelayCommand]
    private async Task DeleteAsync()
    {
        if (Customer == null) return;
        bool confirm = await Shell.Current.DisplayAlert("Delete Customer", $"Delete {Customer.Name}?", "Delete", "Cancel");
        if (!confirm) return;

        try
        {
            await _apiService.DeleteCustomerAsync(Customer.Id);
            _listCache.Remove(CacheKey);
            if (Views.MainLayout.Current?.IsWideLayout == true)
            {
                Views.MainLayout.Current.ClearDetail();
                WeakReferenceMessenger.Default.Send(new DataChangedMessage("customer"));
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
    private async Task PickPhotoAsync()
    {
        if (Customer == null) return;
        var uploaded = await _photoService.PickAndUploadMultipleCustomerPhotosAsync(Customer.Id, (current, total) =>
            UploadStatus = total > 1 ? $"Uploading {current} of {total}..." : "Uploading...");
        UploadStatus = string.Empty;
        if (uploaded.Count > 0) await LoadCustomerAsync();
    }

    [RelayCommand]
    private async Task TakePhotoAsync()
    {
        if (Customer == null) return;
        var photo = await _photoService.CaptureAndUploadCustomerPhotoAsync(Customer.Id);
        if (photo != null) await LoadCustomerAsync();
    }

    [RelayCommand]
    private async Task DeletePhotoAsync(PhotoResponse photo)
    {
        bool confirm = await Shell.Current.DisplayAlert("Delete Photo", "Delete this photo?", "Delete", "Cancel");
        if (!confirm) return;
        await _apiService.DeletePhotoAsync(photo.Id);
        await LoadCustomerAsync();
    }

    [RelayCommand]
    private async Task ViewPhotosAsync(PhotoDisplayModel photo)
    {
        if (Customer == null || Photos.Count == 0) return;
        var index = Photos.IndexOf(photo);
        if (index < 0) index = 0;
        await PhotoViewerLauncher.ShowAsync($"{Customer.Name} Pictures", Photos, index);
    }

    [RelayCommand]
    private async Task ViewLocationPhotosAsync()
    {
        if (Customer?.Address == null) return;
        await Shell.Current.GoToAsync($"locationPhotos?address={Uri.EscapeDataString(Customer.Address)}&excludeCustomerId={CustomerId}");
    }

    [RelayCommand]
    private void ViewJob(JobBriefResponse job)
    {
        var id = job.Id.ToString();
        WeakReferenceMessenger.Default.Send(new ShowDetailMessage(new DetailRequest
        {
            Route = "jobDetail",
            Properties = new() { ["JobId"] = id },
            QueryParams = new() { ["id"] = id },
            SwitchTabIndex = 1
        }));
    }

    [RelayCommand]
    private void AddJob()
    {
        WeakReferenceMessenger.Default.Send(new ShowDetailMessage(new DetailRequest
        {
            Route = "jobEdit",
            Properties = new() { ["InitialCustomerId"] = CustomerId! },
            QueryParams = new() { ["customerId"] = CustomerId! }
        }));
    }

    // Releasing the long-press that opened the options popup still registers
    // as a tap (the tap recognizer doesn't disqualify long holds), which would
    // open Maps over the popup — swallow taps while the popup is up.
    private bool _addressOptionsOpen;

    [RelayCommand]
    private async Task OpenAddressInMapsAsync()
    {
        if (_addressOptionsOpen) return;
        var address = Customer?.Address;
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
        var address = Customer?.Address;
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
        var address = Customer?.Address;
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
}
