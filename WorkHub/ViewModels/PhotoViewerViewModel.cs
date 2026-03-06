using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using WorkHub.Models;
using WorkHub.Services;

namespace WorkHub.ViewModels;

[QueryProperty(nameof(EntityType), "entityType")]
[QueryProperty(nameof(EntityId), "entityId")]
[QueryProperty(nameof(StartIndex), "startIndex")]
public partial class PhotoViewerViewModel : BaseViewModel
{
    private readonly ApiService _apiService;

    [ObservableProperty]
    private string _entityType = string.Empty;

    [ObservableProperty]
    private string _entityId = string.Empty;

    [ObservableProperty]
    private string _startIndex = "0";

    [ObservableProperty]
    private ObservableCollection<PhotoResponse> _photos = new();

    [ObservableProperty]
    private int _currentIndex;

    [ObservableProperty]
    private string _title = "Photos";

    public PhotoViewerViewModel(ApiService apiService)
    {
        _apiService = apiService;
    }

    partial void OnEntityIdChanged(string value)
    {
        if (!string.IsNullOrEmpty(value))
            LoadPhotosCommand.Execute(null);
    }

    [RelayCommand]
    private async Task LoadPhotosAsync()
    {
        await LoadAsync(async () =>
        {
            if (EntityType == "customer" && Guid.TryParse(EntityId, out var custId))
            {
                var customer = await _apiService.GetCustomerAsync(custId);
                Photos = new ObservableCollection<PhotoResponse>(customer?.Photos ?? new());
                Title = $"{customer?.Name} Photos";
            }
            else if (EntityType == "job" && Guid.TryParse(EntityId, out var jobId))
            {
                var job = await _apiService.GetJobAsync(jobId);
                Photos = new ObservableCollection<PhotoResponse>(job?.Photos ?? new());
                Title = $"{job?.Title} Photos";
            }

            if (int.TryParse(StartIndex, out var idx) && idx >= 0 && idx < Photos.Count)
                CurrentIndex = idx;

            if (Photos.Count == 0) SetEmpty();
            else SetContent();
        });
    }

    [RelayCommand]
    private async Task DeleteCurrentPhotoAsync()
    {
        if (CurrentIndex < 0 || CurrentIndex >= Photos.Count) return;
        var photo = Photos[CurrentIndex];
        bool confirm = await Shell.Current.DisplayAlert("Delete Photo", "Delete this photo?", "Delete", "Cancel");
        if (!confirm) return;

        try
        {
            await _apiService.DeletePhotoAsync(photo.Id);
            Photos.RemoveAt(CurrentIndex);
            if (CurrentIndex >= Photos.Count && Photos.Count > 0)
                CurrentIndex = Photos.Count - 1;
            if (Photos.Count == 0)
                await Shell.Current.GoToAsync("..");
        }
        catch (Exception ex)
        {
            await Shell.Current.DisplayAlert("Error", ex.Message, "OK");
        }
    }
}
