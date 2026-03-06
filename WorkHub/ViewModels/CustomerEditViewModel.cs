using System.Collections.ObjectModel;
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

    public ObservableCollection<ContactEntry> PhoneEntries { get; } = [];
    public ObservableCollection<ContactEntry> EmailEntries { get; } = [];

    public string[] PhoneLabelOptions { get; } = ["Mobile", "Home", "Work", "Other"];
    public string[] EmailLabelOptions { get; } = ["Personal", "Work", "Other"];

    public CustomerEditViewModel(ApiService apiService)
    {
        _apiService = apiService;
        // Start with one empty phone and email entry
        PhoneEntries.Add(new ContactEntry { Label = "Mobile" });
        EmailEntries.Add(new ContactEntry { Label = "Personal" });
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
                ParseAddress(customer.Address);
                Notes = customer.Notes ?? string.Empty;

                PhoneEntries.Clear();
                EmailEntries.Clear();

                if (customer.Contacts?.Count > 0)
                {
                    foreach (var c in customer.Contacts.Where(c => c.Type == "phone"))
                        PhoneEntries.Add(new ContactEntry { Label = c.Label, Value = c.Value, IsPrimary = c.IsPrimary });
                    foreach (var c in customer.Contacts.Where(c => c.Type == "email"))
                        EmailEntries.Add(new ContactEntry { Label = c.Label, Value = c.Value, IsPrimary = c.IsPrimary });
                }

                if (PhoneEntries.Count == 0)
                    PhoneEntries.Add(new ContactEntry { Label = "Mobile" });
                if (EmailEntries.Count == 0)
                    EmailEntries.Add(new ContactEntry { Label = "Personal" });
            }
        });
    }

    [RelayCommand]
    private void AddPhone()
    {
        PhoneEntries.Add(new ContactEntry { Label = "Mobile" });
    }

    [RelayCommand]
    private void RemovePhone(ContactEntry entry)
    {
        if (PhoneEntries.Count > 1)
            PhoneEntries.Remove(entry);
        else
        {
            entry.Value = string.Empty;
            entry.Label = "Mobile";
        }
    }

    [RelayCommand]
    private void AddEmail()
    {
        EmailEntries.Add(new ContactEntry { Label = "Personal" });
    }

    [RelayCommand]
    private void RemoveEmail(ContactEntry entry)
    {
        if (EmailEntries.Count > 1)
            EmailEntries.Remove(entry);
        else
        {
            entry.Value = string.Empty;
            entry.Label = "Personal";
        }
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
            var contacts = BuildContacts();

            if (IsNew)
            {
                var request = new CreateCustomerRequest
                {
                    Name = Name.Trim(),
                    Address = BuildAddress(),
                    Notes = string.IsNullOrWhiteSpace(Notes) ? null : Notes.Trim(),
                    Contacts = contacts.Count > 0 ? contacts : null,
                };
                await _apiService.CreateCustomerAsync(request);
            }
            else
            {
                var request = new UpdateCustomerRequest
                {
                    Name = Name.Trim(),
                    Address = BuildAddress(),
                    Notes = string.IsNullOrWhiteSpace(Notes) ? null : Notes.Trim(),
                    Contacts = contacts,
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

    private List<CustomerContactRequest> BuildContacts()
    {
        var contacts = new List<CustomerContactRequest>();
        foreach (var p in PhoneEntries.Where(e => !string.IsNullOrWhiteSpace(e.Value)))
            contacts.Add(new CustomerContactRequest { Type = "phone", Label = p.Label, Value = p.Value.Trim(), IsPrimary = p.IsPrimary });
        foreach (var e in EmailEntries.Where(e => !string.IsNullOrWhiteSpace(e.Value)))
            contacts.Add(new CustomerContactRequest { Type = "email", Label = e.Label, Value = e.Value.Trim(), IsPrimary = e.IsPrimary });
        return contacts;
    }

    private void ParseAddress(string? address)
    {
        if (string.IsNullOrWhiteSpace(address))
        {
            Street = City = State = Zip = string.Empty;
            return;
        }

        var lines = address.Split('\n', StringSplitOptions.TrimEntries);
        if (lines.Length >= 2)
        {
            Street = lines[0];
            ParseCityStateZip(lines[1]);
        }
        else
        {
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

public partial class ContactEntry : ObservableObject
{
    [ObservableProperty]
    private string _label = string.Empty;

    [ObservableProperty]
    private string _value = string.Empty;

    [ObservableProperty]
    private bool _isPrimary;
}
