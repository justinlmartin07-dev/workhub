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
    private string _street = string.Empty;

    [ObservableProperty]
    private string _city = string.Empty;

    [ObservableProperty]
    private string _state = string.Empty;

    [ObservableProperty]
    private string _zip = string.Empty;

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
                ParseAddress(customer.Address);
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
                    Address = BuildAddress(),
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
                    Address = BuildAddress(),
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

    private void ParseAddress(string? address)
    {
        if (string.IsNullOrWhiteSpace(address))
        {
            Street = City = State = Zip = string.Empty;
            return;
        }

        // Try to parse "Street\nCity, State Zip" or "Street, City, State Zip"
        var lines = address.Split('\n', StringSplitOptions.TrimEntries);
        if (lines.Length >= 2)
        {
            Street = lines[0];
            ParseCityStateZip(lines[1]);
        }
        else
        {
            // Single line: try "Street, City, State Zip"
            var parts = address.Split(',', StringSplitOptions.TrimEntries);
            if (parts.Length >= 3)
            {
                Street = parts[0];
                City = parts[1];
                ParseStateZip(parts[2]);
            }
            else if (parts.Length == 2)
            {
                Street = parts[0];
                ParseCityStateZip(parts[1]);
            }
            else
            {
                Street = address;
            }
        }
    }

    private void ParseCityStateZip(string line)
    {
        var parts = line.Split(',', StringSplitOptions.TrimEntries);
        if (parts.Length >= 2)
        {
            City = parts[0];
            ParseStateZip(parts[1]);
        }
        else
        {
            ParseStateZip(line);
        }
    }

    private void ParseStateZip(string text)
    {
        var tokens = text.Trim().Split(' ', StringSplitOptions.RemoveEmptyEntries);
        if (tokens.Length >= 2)
        {
            State = tokens[0];
            Zip = tokens[1];
        }
        else if (tokens.Length == 1)
        {
            State = tokens[0];
        }
    }

    private string? BuildAddress()
    {
        var street = Street?.Trim();
        var city = City?.Trim();
        var state = State?.Trim();
        var zip = Zip?.Trim();

        if (string.IsNullOrEmpty(street) && string.IsNullOrEmpty(city) && string.IsNullOrEmpty(state) && string.IsNullOrEmpty(zip))
            return null;

        var cityStateZip = string.Empty;
        if (!string.IsNullOrEmpty(city) || !string.IsNullOrEmpty(state) || !string.IsNullOrEmpty(zip))
        {
            var stateZip = $"{state} {zip}".Trim();
            cityStateZip = !string.IsNullOrEmpty(city) && !string.IsNullOrEmpty(stateZip)
                ? $"{city}, {stateZip}"
                : $"{city}{stateZip}";
        }

        if (!string.IsNullOrEmpty(street) && !string.IsNullOrEmpty(cityStateZip))
            return $"{street}\n{cityStateZip}";
        return !string.IsNullOrEmpty(street) ? street : cityStateZip;
    }
}
