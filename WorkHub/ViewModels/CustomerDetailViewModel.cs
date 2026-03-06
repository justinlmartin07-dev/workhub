using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using CommunityToolkit.Mvvm.Messaging;
using WorkHub.Messages;
using WorkHub.Models;
using WorkHub.Services;

namespace WorkHub.ViewModels;

[QueryProperty(nameof(CustomerId), "id")]
public partial class CustomerDetailViewModel : BaseViewModel
{
    private readonly ApiService _apiService;
    private readonly PhotoService _photoService;

    [ObservableProperty]
    private string? _customerId;

    [ObservableProperty]
    private CustomerResponse? _customer;

    [ObservableProperty]
    private int _locationPhotoCount;

    public CustomerDetailViewModel(ApiService apiService, PhotoService photoService)
    {
        _apiService = apiService;
        _photoService = photoService;
    }

    partial void OnCustomerIdChanged(string? value)
    {
        if (Guid.TryParse(value, out _))
            LoadCustomerCommand.Execute(null);
    }

    [RelayCommand]
    public async Task LoadCustomerAsync()
    {
        if (!Guid.TryParse(CustomerId, out var id)) return;
        await LoadAsync(async () =>
        {
            Customer = await _apiService.GetCustomerAsync(id);
            if (Customer?.Address != null)
            {
                LocationPhotoCount = await _apiService.GetLocationPhotoCountAsync(Customer.Address, excludeCustomerId: id);
            }
        });
    }

    [RelayCommand]
    private async Task EditAsync()
    {
        await Shell.Current.GoToAsync($"customerEdit?id={CustomerId}");
    }

    [RelayCommand]
    private async Task DeleteAsync()
    {
        if (Customer == null) return;
        bool confirm = await Shell.Current.DisplayAlert("Delete Customer", $"Delete {Customer.Name}?", "Delete", "Cancel");
        if (!confirm) return;

        try
        {
            await _apiService.DeleteCustomerAsync(Customer.Id);
            await Shell.Current.GoToAsync("..");
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
        var photo = await _photoService.PickAndUploadCustomerPhotoAsync(Customer.Id);
        if (photo != null) await LoadCustomerAsync();
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
    private async Task ViewPhotosAsync(PhotoResponse photo)
    {
        if (Customer?.Photos == null) return;
        var index = Customer.Photos.IndexOf(photo);
        await Shell.Current.GoToAsync($"photoViewer?entityType=customer&entityId={CustomerId}&startIndex={index}");
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
    private async Task AddJobAsync()
    {
        await Shell.Current.GoToAsync($"jobEdit?customerId={CustomerId}");
    }

    [RelayCommand]
    private async Task CallPhoneAsync()
    {
        if (!string.IsNullOrEmpty(Customer?.Phone))
        {
            try { PhoneDialer.Open(Customer.Phone); }
            catch { }
        }
    }

    [RelayCommand]
    private async Task SendEmailAsync()
    {
        if (!string.IsNullOrEmpty(Customer?.Email))
        {
            try { await Launcher.OpenAsync($"mailto:{Customer.Email}"); }
            catch { }
        }
    }
}
