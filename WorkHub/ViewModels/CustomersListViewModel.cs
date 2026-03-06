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

    private int _currentPage = 1;
    private int _totalPages = 1;

    public CustomersListViewModel(ApiService apiService)
    {
        _apiService = apiService;
    }

    [RelayCommand]
    public async Task LoadCustomersAsync()
    {
        _currentPage = 1;
        await LoadAsync(async () =>
        {
            var result = await _apiService.GetCustomersAsync(SearchText, _currentPage);
            _totalPages = result.TotalPages;
            Customers = new ObservableCollection<CustomerResponse>(result.Items);
            if (Customers.Count == 0) SetEmpty();
            else SetContent();
        });
    }

    [RelayCommand]
    private async Task LoadMoreAsync()
    {
        if (IsBusy || _currentPage >= _totalPages) return;
        _currentPage++;
        await LoadAsync(async () =>
        {
            var result = await _apiService.GetCustomersAsync(SearchText, _currentPage);
            foreach (var customer in result.Items)
                Customers.Add(customer);
        }, showLoading: false);
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
