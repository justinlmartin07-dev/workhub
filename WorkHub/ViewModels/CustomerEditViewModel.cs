using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using CommunityToolkit.Mvvm.Messaging;
using WorkHub.Messages;
using WorkHub.Models;
using WorkHub.Services;

namespace WorkHub.ViewModels;

[QueryProperty(nameof(CustomerId), "id")]
public partial class CustomerEditViewModel : BaseViewModel, IHasUnsavedChanges
{
    private readonly ApiService _apiService;
    private readonly LocationBiasService _locationBias;

    private string? _addressSessionToken;

    private string _origName = string.Empty;
    private string _origCompanyName = string.Empty;
    private string _origStreet = string.Empty;
    private string _origCity = string.Empty;
    private string _origState = string.Empty;
    private string _origZip = string.Empty;
    private string _origNotes = string.Empty;
    private string _origContactsHash = string.Empty;

    public bool HasUnsavedChanges =>
        Name != _origName || CompanyName != _origCompanyName ||
        Street != _origStreet || City != _origCity ||
        State != _origState || Zip != _origZip || Notes != _origNotes ||
        GetContactsHash() != _origContactsHash;

    private void SnapshotOriginal()
    {
        _origName = Name;
        _origCompanyName = CompanyName;
        _origStreet = Street;
        _origCity = City;
        _origState = State;
        _origZip = Zip;
        _origNotes = Notes;
        _origContactsHash = GetContactsHash();
    }

    private string GetContactsHash()
    {
        var parts = new List<string>();
        foreach (var p in PhoneEntries)
            parts.Add($"phone:{p.Label}:{p.Value}:{p.IsPrimary}");
        foreach (var e in EmailEntries)
            parts.Add($"email:{e.Label}:{e.Value}:{e.IsPrimary}");
        return string.Join("|", parts);
    }

    [ObservableProperty]
    private string? _customerId;

    [ObservableProperty]
    private string _name = string.Empty;

    [ObservableProperty]
    private string _companyName = string.Empty;

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

    [ObservableProperty]
    private ObservableCollection<AddressSuggestionResponse> _addressSuggestions = new();

    [ObservableProperty]
    private bool _showAddressSuggestions;

    private CancellationTokenSource? _addressCts;
    private bool _skipAddressSearch;

    [ObservableProperty]
    private ObservableCollection<string> _phoneLabelOptions = new(["Mobile", "Home", "Work", "Office", "Main", "Other"]);

    [ObservableProperty]
    private ObservableCollection<string> _emailLabelOptions = new(["Personal", "Work", "Other"]);

    public CustomerEditViewModel(ApiService apiService, LocationBiasService locationBias)
    {
        _apiService = apiService;
        _locationBias = locationBias;
        // Start with one empty phone and email entry
        PhoneEntries.Add(new ContactEntry { Label = "Mobile" });
        EmailEntries.Add(new ContactEntry { Label = "Work" });
        SnapshotOriginal();
        _ = LoadContactLabelsAsync();
    }

    private async Task LoadContactLabelsAsync()
    {
        try
        {
            var labels = await _apiService.GetContactLabelsAsync();
            var phone = labels.Where(l => l.Type == "phone").Select(l => l.Label).ToList();
            var email = labels.Where(l => l.Type == "email").Select(l => l.Label).ToList();
            if (phone.Count > 0) PhoneLabelOptions = new ObservableCollection<string>(phone);
            if (email.Count > 0) EmailLabelOptions = new ObservableCollection<string>(email);
        }
        catch { /* keep defaults */ }
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
                CompanyName = customer.CompanyName ?? string.Empty;
                _skipAddressSearch = true;
                ParseAddress(customer.Address);
                _skipAddressSearch = false;
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
                    EmailEntries.Add(new ContactEntry { Label = "Work" });

                SnapshotOriginal();
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
        EmailEntries.Add(new ContactEntry { Label = "Work" });
    }

    [RelayCommand]
    private void RemoveEmail(ContactEntry entry)
    {
        if (EmailEntries.Count > 1)
            EmailEntries.Remove(entry);
        else
        {
            entry.Value = string.Empty;
            entry.Label = "Work";
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
                    CompanyName = CompanyName.Trim(),
                    Address = BuildAddress(),
                    Notes = string.IsNullOrWhiteSpace(Notes) ? null : Notes.Trim(),
                    Contacts = contacts.Count > 0 ? contacts : null,
                };
                var created = await _apiService.CreateCustomerAsync(request);
                if (created != null)
                    CustomerId = created.Id.ToString();
            }
            else
            {
                var request = new UpdateCustomerRequest
                {
                    Name = Name.Trim(),
                    // Always sent — an empty string clears the company on the server
                    CompanyName = CompanyName.Trim(),
                    Address = BuildAddress(),
                    Notes = string.IsNullOrWhiteSpace(Notes) ? null : Notes.Trim(),
                    Contacts = contacts,
                };
                await _apiService.UpdateCustomerAsync(Guid.Parse(CustomerId!), request);
            }
            SnapshotOriginal();
            WeakReferenceMessenger.Default.Send(new DataChangedMessage("customer"));
            NavigateBackToDetail();
            if (!string.IsNullOrEmpty(CustomerId))
                WeakReferenceMessenger.Default.Send(new SelectListItemMessage(new SelectListItemRequest { ItemId = CustomerId, TabIndex = 0 }));
        });
    }

    partial void OnStreetChanged(string value)
    {
        if (_skipAddressSearch) return;
        _addressCts?.Cancel();
        if (string.IsNullOrWhiteSpace(value) || value.Length < 3)
        {
            ShowAddressSuggestions = false;
            return;
        }

        // Start a new Places session on the first keystroke of a fresh search.
        _addressSessionToken ??= Guid.NewGuid().ToString("N");

        _addressCts = new CancellationTokenSource();
        var token = _addressCts.Token;
        MainThread.BeginInvokeOnMainThread(async () =>
        {
            try
            {
                await Task.Delay(200, token);
                var bias = await _locationBias.GetCenterAsync();
                if (token.IsCancellationRequested) return;
                var results = await _apiService.GetAddressSuggestionsAsync(
                    value, bias, _locationBias.RadiusMeters, _addressSessionToken, token);
                if (!token.IsCancellationRequested)
                {
                    AddressSuggestions = new ObservableCollection<AddressSuggestionResponse>(results);
                    ShowAddressSuggestions = results.Count > 0;
                }
            }
            catch (OperationCanceledException) { }
        });
    }

    [RelayCommand]
    private async Task SelectAddressSuggestionAsync(AddressSuggestionResponse suggestion)
    {
        ShowAddressSuggestions = false;
        _addressCts?.Cancel();

        var details = await _apiService.GetAddressDetailsAsync(suggestion.PlaceId, _addressSessionToken);
        // Selection closes the Places session; the next keystroke starts a new one.
        _addressSessionToken = null;
        if (details != null)
        {
            _skipAddressSearch = true;
            Street = details.Street;
            City = details.City;
            State = details.State;
            Zip = details.Zip;
            _skipAddressSearch = false;
        }
    }

    [RelayCommand]
    private async Task CancelAsync()
    {
        if (HasUnsavedChanges)
        {
            var discard = await Application.Current!.Windows[0].Page!.DisplayAlert(
                "Unsaved Changes", "You have unsaved changes. Discard them?", "Discard", "Stay");
            if (!discard) return;
        }
        SnapshotOriginal(); // Clear dirty state so NavigateBackToDetail doesn't re-trigger
        NavigateBackToDetail();
    }

    private async void NavigateBackToDetail()
    {
        if (Views.MainLayout.Current?.IsWideLayout == true)
        {
            if (!string.IsNullOrEmpty(CustomerId))
            {
                WeakReferenceMessenger.Default.Send(new ShowDetailMessage(new DetailRequest
                {
                    Route = "customerDetail",
                    Properties = new() { ["CustomerId"] = CustomerId },
                    QueryParams = new() { ["id"] = CustomerId }
                }));
            }
            else
            {
                Views.MainLayout.Current.ClearDetail();
            }
        }
        else
        {
            await Shell.Current.GoToAsync("..");
        }
    }

    private List<CustomerContactRequest> BuildContacts()
    {
        var contacts = new List<CustomerContactRequest>();
        foreach (var p in PhoneEntries.Where(e => !string.IsNullOrWhiteSpace(e.Value)))
            contacts.Add(new CustomerContactRequest { Type = "phone", Label = string.IsNullOrEmpty(p.Label) ? "Other" : p.Label, Value = p.Value.Trim(), IsPrimary = p.IsPrimary });
        foreach (var e in EmailEntries.Where(e => !string.IsNullOrWhiteSpace(e.Value)))
            contacts.Add(new CustomerContactRequest { Type = "email", Label = string.IsNullOrEmpty(e.Label) ? "Other" : e.Label, Value = e.Value.Trim(), IsPrimary = e.IsPrimary });
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
