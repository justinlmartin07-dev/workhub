using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using WorkHub.Models;
using WorkHub.Services;

namespace WorkHub.ViewModels;

[QueryProperty(nameof(Address), "address")]
[QueryProperty(nameof(ExcludeCustomerId), "excludeCustomerId")]
[QueryProperty(nameof(ExcludeJobId), "excludeJobId")]
public partial class LocationPhotosViewModel : BaseViewModel
{
    private readonly ApiService _apiService;

    [ObservableProperty]
    private string _address = string.Empty;

    [ObservableProperty]
    private string? _excludeCustomerId;

    [ObservableProperty]
    private string? _excludeJobId;

    [ObservableProperty]
    private ObservableCollection<LocationPhotoGroupResponse> _groups = new();

    public LocationPhotosViewModel(ApiService apiService)
    {
        _apiService = apiService;
    }

    partial void OnAddressChanged(string value)
    {
        if (!string.IsNullOrEmpty(value))
            LoadPhotosCommand.Execute(null);
    }

    [RelayCommand]
    private async Task LoadPhotosAsync()
    {
        await LoadAsync(async () =>
        {
            Guid? exCust = Guid.TryParse(ExcludeCustomerId, out var c) ? c : null;
            Guid? exJob = Guid.TryParse(ExcludeJobId, out var j) ? j : null;
            var groups = await _apiService.GetLocationPhotosAsync(Address, exCust, exJob);
            Groups = new ObservableCollection<LocationPhotoGroupResponse>(groups);
            if (Groups.Count == 0) SetEmpty();
            else SetContent();
        });
    }
}
