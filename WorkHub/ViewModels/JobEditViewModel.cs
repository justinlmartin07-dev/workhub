using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using CommunityToolkit.Mvvm.Messaging;
using WorkHub.Messages;
using WorkHub.Models;
using WorkHub.Services;

namespace WorkHub.ViewModels;

[QueryProperty(nameof(JobId), "id")]
[QueryProperty(nameof(InitialCustomerId), "customerId")]
public partial class JobEditViewModel : BaseViewModel, IHasUnsavedChanges
{
    private readonly ApiService _apiService;
    private readonly LocationBiasService _locationBias;

    private string? _addressSessionToken;

    private string _origTitle = string.Empty;
    private string _origPriority = "Medium";
    private string _origStreet = string.Empty;
    private string _origCity = string.Empty;
    private string _origState = string.Empty;
    private string _origZip = string.Empty;
    private string _origScopeNotes = string.Empty;
    private Guid? _origCustomerId;
    private Guid? _origMainContactId;

    // The None sentinel normalized away: null when nothing (or None) is picked.
    private Guid? CurrentMainContactId =>
        SelectedMainContact is { } mc && mc.Id != Guid.Empty ? mc.Id : null;

    public bool HasUnsavedChanges =>
        Title != _origTitle || SelectedPriority != _origPriority ||
        Street != _origStreet || City != _origCity || State != _origState || Zip != _origZip ||
        ScopeNotes != _origScopeNotes || SelectedCustomer?.Id != _origCustomerId ||
        CurrentMainContactId != _origMainContactId;

    private void SnapshotOriginal()
    {
        _origTitle = Title;
        _origPriority = SelectedPriority;
        _origStreet = Street;
        _origCity = City;
        _origState = State;
        _origZip = Zip;
        _origScopeNotes = ScopeNotes;
        _origCustomerId = SelectedCustomer?.Id;
        _origMainContactId = CurrentMainContactId;
    }

    [ObservableProperty]
    private string? _jobId;

    [ObservableProperty]
    private string? _initialCustomerId;

    [ObservableProperty]
    private string _title = string.Empty;

    [ObservableProperty]
    private string _selectedPriority = "Medium";

    [ObservableProperty]
    private double _prioritySliderValue = 1;

    partial void OnPrioritySliderValueChanged(double value)
    {
        SelectedPriority = (int)Math.Round(value) switch
        {
            0 => "Low",
            1 => "Medium",
            2 => "High",
            _ => "Medium"
        };
    }

    [ObservableProperty]
    private string _scopeNotes = string.Empty;

    [ObservableProperty]
    private string _street = string.Empty;

    [ObservableProperty]
    private string _city = string.Empty;

    [ObservableProperty]
    private string _state = string.Empty;

    [ObservableProperty]
    private string _zip = string.Empty;

    [ObservableProperty]
    private ObservableCollection<AddressSuggestionResponse> _addressSuggestions = new();

    [ObservableProperty]
    private bool _showAddressSuggestions;

    private CancellationTokenSource? _addressCts;
    private bool _skipAddressSearch;

    [ObservableProperty]
    private ObservableCollection<CustomerResponse> _allCustomers = new();

    [ObservableProperty]
    private ObservableCollection<CustomerResponse> _filteredCustomers = new();

    [ObservableProperty]
    private CustomerResponse? _selectedCustomer;

    private static readonly ContactPersonResponse NoneContact = new() { Id = Guid.Empty, Name = "None" };

    [ObservableProperty]
    private ObservableCollection<ContactPersonResponse> _mainContactOptions = new([NoneContact]);

    [ObservableProperty]
    private ContactPersonResponse? _selectedMainContact = NoneContact;

    partial void OnSelectedCustomerChanged(CustomerResponse? value)
    {
        // Customer changed → any previously picked person belongs to the old
        // customer; rebuild the options and reset to None.
        var options = new List<ContactPersonResponse> { NoneContact };
        if (value?.Persons != null)
            options.AddRange(value.Persons);
        MainContactOptions = new ObservableCollection<ContactPersonResponse>(options);
        SelectedMainContact = NoneContact;
    }

    [ObservableProperty]
    private string _customerSearchText = string.Empty;

    [ObservableProperty]
    private bool _isCustomerPickerOpen;

    [ObservableProperty]
    private bool _isNew = true;

    [ObservableProperty]
    private string _pageTitle = "New Job";

    public List<string> PriorityOptions { get; } = new() { "Low", "Medium", "High" };

    private bool _dataLoaded;

    public JobEditViewModel(ApiService apiService, LocationBiasService locationBias)
    {
        _apiService = apiService;
        _locationBias = locationBias;
        // Deferred load for when no property change triggers LoadData (new job from jobs list)
        MainThread.BeginInvokeOnMainThread(async () =>
        {
            await Task.Yield();
            if (!_dataLoaded && IsNew)
                await LoadDataAsync();
        });
    }

    partial void OnJobIdChanged(string? value)
    {
        if (Guid.TryParse(value, out _))
        {
            IsNew = false;
            PageTitle = "Edit Job";
            LoadDataCommand.Execute(null);
        }
    }

    partial void OnInitialCustomerIdChanged(string? value)
    {
        if (IsNew)
            LoadDataCommand.Execute(null);
    }

    private CancellationTokenSource? _searchCts;

    partial void OnCustomerSearchTextChanged(string value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            _searchCts?.Cancel();
            FilterCustomers();
            return;
        }

        _searchCts?.Cancel();
        _searchCts = new CancellationTokenSource();
        _ = DebounceSearchAsync(_searchCts.Token);
    }

    private async Task DebounceSearchAsync(CancellationToken ct)
    {
        try { await Task.Delay(200, ct); }
        catch (OperationCanceledException) { return; }
        FilterCustomers();
    }

    private void FilterCustomers()
    {
        if (string.IsNullOrWhiteSpace(CustomerSearchText))
        {
            FilteredCustomers = new ObservableCollection<CustomerResponse>(AllCustomers);
        }
        else
        {
            var search = CustomerSearchText.ToLower();
            FilteredCustomers = new ObservableCollection<CustomerResponse>(
                AllCustomers.Where(c =>
                    c.Name.ToLower().Contains(search) ||
                    (c.CompanyName?.ToLower().Contains(search) ?? false) ||
                    (c.Contacts?.Any(ct => ct.Value.ToLower().Contains(search)) ?? false) ||
                    (c.Address?.ToLower().Contains(search) ?? false)));
        }
    }

    [RelayCommand]
    private async Task LoadDataAsync()
    {
        _dataLoaded = true;
        await LoadAsync(async () =>
        {
            if (IsNew)
            {
                await LoadAllCustomersAsync();
                if (Guid.TryParse(InitialCustomerId, out var custId))
                    SelectedCustomer = AllCustomers.FirstOrDefault(c => c.Id == custId);
            }
            else if (Guid.TryParse(JobId, out var jobId))
            {
                var job = await _apiService.GetJobAsync(jobId);
                if (job != null)
                {
                    Title = job.Title;
                    SelectedPriority = job.Priority;
                    PrioritySliderValue = job.Priority switch { "Low" => 0, "High" => 2, _ => 1 };
                    // Load customer for "Use Customer Address" button and the contact picker
                    var customer = await _apiService.GetCustomerAsync(job.CustomerId);
                    if (customer != null)
                        SelectedCustomer = customer;
                    // After OnSelectedCustomerChanged rebuilt the options — select by id
                    // from the current list (the Picker matches by reference)
                    SelectedMainContact = MainContactOptions.FirstOrDefault(p => p.Id == job.MainContactId) ?? NoneContact;
                    ScopeNotes = job.ScopeNotes ?? string.Empty;
                    _skipAddressSearch = true;
                    ParseAddress(job.Address);
                    _skipAddressSearch = false;
                    SnapshotOriginal();
                }
            }
            else
            {
                SnapshotOriginal();
            }
        });
    }

    private async Task LoadAllCustomersAsync()
    {
        var all = new List<CustomerResponse>();
        int page = 1;
        int totalPages;
        do
        {
            var result = await _apiService.GetCustomersAsync(page: page, pageSize: 100);
            all.AddRange(result.Items);
            totalPages = result.TotalPages;
            page++;
        } while (page <= totalPages);

        AllCustomers = new ObservableCollection<CustomerResponse>(all.OrderBy(c => c.DisplayName));
        FilterCustomers();
    }

    [RelayCommand]
    private void ToggleCustomerPicker()
    {
        IsCustomerPickerOpen = !IsCustomerPickerOpen;
        if (IsCustomerPickerOpen)
        {
            CustomerSearchText = string.Empty;
            FilterCustomers();
        }
    }

    [RelayCommand]
    private void PickCustomer(CustomerResponse customer)
    {
        SelectedCustomer = customer;
        IsCustomerPickerOpen = false;
    }

    [RelayCommand]
    private void UseCustomerAddress()
    {
        var address = SelectedCustomer?.Address;
        if (string.IsNullOrWhiteSpace(address)) return;
        _skipAddressSearch = true;
        ParseAddress(address);
        _skipAddressSearch = false;
    }

    [RelayCommand]
    private async Task SaveAsync()
    {
        if (string.IsNullOrWhiteSpace(Title))
        {
            ErrorMessage = "Title is required";
            HasError = true;
            return;
        }

        await LoadAsync(async () =>
        {
            if (IsNew)
            {
                if (SelectedCustomer == null)
                {
                    throw new Exception("Please select a customer");
                }
                var request = new CreateJobRequest
                {
                    CustomerId = SelectedCustomer.Id,
                    Title = Title.Trim(),
                    Priority = SelectedPriority,
                    ScopeNotes = string.IsNullOrWhiteSpace(ScopeNotes) ? null : ScopeNotes.Trim(),
                    Address = BuildAddress(),
                    MainContactId = CurrentMainContactId
                };
                var created = await _apiService.CreateJobAsync(request);
                if (created != null)
                    JobId = created.Id.ToString();
            }
            else
            {
                var request = new UpdateJobRequest
                {
                    Title = Title.Trim(),
                    Priority = SelectedPriority,
                    ScopeNotes = string.IsNullOrWhiteSpace(ScopeNotes) ? null : ScopeNotes.Trim(),
                    Address = BuildAddress(),
                    // Always sent — Guid.Empty tells the server to clear the contact
                    MainContactId = CurrentMainContactId ?? Guid.Empty
                };
                await _apiService.UpdateJobAsync(Guid.Parse(JobId!), request);
            }
            SnapshotOriginal();
            WeakReferenceMessenger.Default.Send(new DataChangedMessage("job"));
            NavigateBackToDetail();
            if (!string.IsNullOrEmpty(JobId))
                WeakReferenceMessenger.Default.Send(new SelectListItemMessage(new SelectListItemRequest { ItemId = JobId, TabIndex = 1 }));
        });
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
            var parts = lines[1].Split(',', StringSplitOptions.TrimEntries);
            if (parts.Length >= 2)
            {
                City = parts[0];
                var tokens = parts[1].Trim().Split(' ', StringSplitOptions.RemoveEmptyEntries);
                State = tokens.Length >= 1 ? tokens[0] : string.Empty;
                Zip = tokens.Length >= 2 ? tokens[1] : string.Empty;
            }
            else
            {
                City = lines[1];
            }
        }
        else
        {
            Street = address;
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

    private async void NavigateBackToDetail()
    {
        if (Views.MainLayout.Current?.IsWideLayout == true)
        {
            if (!string.IsNullOrEmpty(JobId))
            {
                WeakReferenceMessenger.Default.Send(new ShowDetailMessage(new DetailRequest
                {
                    Route = "jobDetail",
                    Properties = new() { ["JobId"] = JobId },
                    QueryParams = new() { ["id"] = JobId }
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
}
