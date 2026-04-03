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
    private CancellationTokenSource? _searchCts;

    public event Action<CustomerResponse>? ScrollToRequested;

    public CustomersListViewModel(ApiService apiService)
    {
        _apiService = apiService;

        WeakReferenceMessenger.Default.Register<SelectListItemMessage>(this, (r, m) =>
        {
            if (m.Value.TabIndex != 0) return; // Only handle Customers tab
            _pendingSelectId = m.Value.ItemId;
            TrySelectPending();
        });

        WeakReferenceMessenger.Default.Register<DataChangedMessage>(this, (r, m) =>
        {
            if (m.Value == "customer")
                MainThread.BeginInvokeOnMainThread(() => LoadCustomersCommand.Execute(null));
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
        if (!Guid.TryParse(_pendingSelectId, out var id))
        {
            SelectedCustomer = null;
            _pendingSelectId = null;
            return;
        }

        var match = Customers.FirstOrDefault(c => c.Id == id);
        if (match != null)
        {
            SelectedCustomer = match;
            ScrollToRequested?.Invoke(match);
            _pendingSelectId = null;
        }
    }

    partial void OnSearchTextChanged(string value)
    {
        _searchCts?.Cancel();
        _searchCts = new CancellationTokenSource();
        var token = _searchCts.Token;
        MainThread.BeginInvokeOnMainThread(async () =>
        {
            try
            {
                await Task.Delay(300, token);
                await LoadCustomersAsync();
            }
            catch (TaskCanceledException) { }
        });
    }

    [RelayCommand]
    private async Task SearchAsync()
    {
        _searchCts?.Cancel();
        await LoadCustomersAsync();
    }

    [RelayCommand]
    private void AddCustomer()
    {
        WeakReferenceMessenger.Default.Send(new ShowDetailMessage(new DetailRequest
        {
            Route = "customerEdit",
            QueryParams = new()
        }));
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
