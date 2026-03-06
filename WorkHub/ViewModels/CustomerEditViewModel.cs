using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using WorkHub.Models;
using WorkHub.Services;

namespace WorkHub.ViewModels;

[QueryProperty(nameof(CustomerId), "id")]
public partial class CustomerEditViewModel : BaseViewModel
{
    private readonly ApiService _apiService;

    [ObservableProperty]
    private string? _customerId;

    [ObservableProperty]
    private string _name = string.Empty;

    [ObservableProperty]
    private string _phone = string.Empty;

    [ObservableProperty]
    private string _email = string.Empty;

    [ObservableProperty]
    private string _address = string.Empty;

    [ObservableProperty]
    private string _notes = string.Empty;

    [ObservableProperty]
    private bool _isNew = true;

    [ObservableProperty]
    private string _pageTitle = "New Customer";

    public CustomerEditViewModel(ApiService apiService)
    {
        _apiService = apiService;
    }

    partial void OnCustomerIdChanged(string? value)
    {
        if (Guid.TryParse(value, out _))
        {
            IsNew = false;
            PageTitle = "Edit Customer";
            LoadCustomerCommand.Execute(null);
        }
    }

    [RelayCommand]
    private async Task LoadCustomerAsync()
    {
        if (!Guid.TryParse(CustomerId, out var id)) return;
        await LoadAsync(async () =>
        {
            var customer = await _apiService.GetCustomerAsync(id);
            if (customer != null)
            {
                Name = customer.Name;
                Phone = customer.Phone ?? string.Empty;
                Email = customer.Email ?? string.Empty;
                Address = customer.Address ?? string.Empty;
                Notes = customer.Notes ?? string.Empty;
            }
        });
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
                var request = new CreateCustomerRequest
                {
                    Name = Name.Trim(),
                    Phone = string.IsNullOrWhiteSpace(Phone) ? null : Phone.Trim(),
                    Email = string.IsNullOrWhiteSpace(Email) ? null : Email.Trim(),
                    Address = string.IsNullOrWhiteSpace(Address) ? null : Address.Trim(),
                    Notes = string.IsNullOrWhiteSpace(Notes) ? null : Notes.Trim()
                };
                await _apiService.CreateCustomerAsync(request);
            }
            else
            {
                var request = new UpdateCustomerRequest
                {
                    Name = Name.Trim(),
                    Phone = string.IsNullOrWhiteSpace(Phone) ? null : Phone.Trim(),
                    Email = string.IsNullOrWhiteSpace(Email) ? null : Email.Trim(),
                    Address = string.IsNullOrWhiteSpace(Address) ? null : Address.Trim(),
                    Notes = string.IsNullOrWhiteSpace(Notes) ? null : Notes.Trim()
                };
                await _apiService.UpdateCustomerAsync(Guid.Parse(CustomerId!), request);
            }
            await Shell.Current.GoToAsync("..");
        });
    }

    [RelayCommand]
    private async Task CancelAsync()
    {
        await Shell.Current.GoToAsync("..");
    }
}
