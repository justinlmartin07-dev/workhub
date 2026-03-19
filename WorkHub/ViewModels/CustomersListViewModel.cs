using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using CommunityToolkit.Mvvm.Messaging;
using WorkHub.Messages;
using WorkHub.Models;
using WorkHub.Services;

namespace WorkHub.ViewModels;

public partial class CustomersListViewModel : BaseViewModel
{
    private readonly ApiService _apiService;

    [ObservableProperty]
    private ObservableCollection<CustomerResponse> _customers = new();

    [ObservableProperty]
    private string _searchText = string.Empty;

    [ObservableProperty]
    private CustomerResponse? _selectedCustomer;

    private string? _pendingSelectId;

    public event Action<CustomerResponse>? ScrollToRequested;

    public CustomersListViewModel(ApiService apiService)
    {
        _apiService = apiService;

        WeakReferenceMessenger.Default.Register<SelectListItemMessage>(this, (r, m) =>
        {
            _pendingSelectId = m.Value;
            TrySelectPending();
        });
    }

    [RelayCommand]
    public async Task LoadCustomersAsync()
    {
        await LoadAsync(async () =>
        {
            var all = new List<CustomerResponse>();
            var page = 1;
            int totalPages;
            do
            {
                var result = await _apiService.GetCustomersAsync(SearchText, page);
                totalPages = result.TotalPages;
                all.AddRange(result.Items);
                page++;
            } while (page <= totalPages);

            Customers = new ObservableCollection<CustomerResponse>(all);
            if (Customers.Count == 0) SetEmpty();
            else SetContent();
            TrySelectPending();
        });
    }

    private void TrySelectPending()
    {
        if (_pendingSelectId == null || Customers.Count == 0) return;
        if (!Guid.TryParse(_pendingSelectId, out var id)) return;

        var match = Customers.FirstOrDefault(c => c.Id == id);
        if (match != null)
        {
            SelectedCustomer = match;
            ScrollToRequested?.Invoke(match);
            _pendingSelectId = null;
        }
    }

    [RelayCommand]
    private async Task SearchAsync()
    {
        await LoadCustomersAsync();
    }

    [RelayCommand]
    private async Task AddCustomerAsync()
    {
        await Shell.Current.GoToAsync("customerEdit");
    }

    [RelayCommand]
    private void SelectCustomer(CustomerResponse customer)
    {
        if (customer == null) return;
        SelectedCustomer = customer;
        var id = customer.Id.ToString();
        WeakReferenceMessenger.Default.Send(new ShowDetailMessage(new DetailRequest
        {
            Route = "customerDetail",
            Properties = new() { ["CustomerId"] = id },
            QueryParams = new() { ["id"] = id }
        }));
    }
}
