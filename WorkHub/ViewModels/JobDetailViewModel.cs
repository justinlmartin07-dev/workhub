using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using WorkHub.Models;
using WorkHub.Services;

namespace WorkHub.ViewModels;

[QueryProperty(nameof(JobId), "id")]
public partial class JobDetailViewModel : BaseViewModel
{
    private readonly ApiService _apiService;
    private readonly PhotoService _photoService;

    [ObservableProperty]
    private string? _jobId;

    [ObservableProperty]
    private JobResponse? _job;

    [ObservableProperty]
    private string _newNoteText = string.Empty;

    [ObservableProperty]
    private int _locationPhotoCount;

    public JobDetailViewModel(ApiService apiService, PhotoService photoService)
    {
        _apiService = apiService;
        _photoService = photoService;
    }

    partial void OnJobIdChanged(string? value)
    {
        if (Guid.TryParse(value, out _))
            LoadJobCommand.Execute(null);
    }

    [RelayCommand]
    public async Task LoadJobAsync()
    {
        if (!Guid.TryParse(JobId, out var id)) return;
        await LoadAsync(async () =>
        {
            Job = await _apiService.GetJobAsync(id);
        });
    }

    [RelayCommand]
    private async Task EditAsync()
    {
        await Shell.Current.GoToAsync($"jobEdit?id={JobId}");
    }

    [RelayCommand]
    private async Task DeleteAsync()
    {
        if (Job == null) return;
        bool confirm = await Shell.Current.DisplayAlert("Delete Job", $"Delete {Job.Title}?", "Delete", "Cancel");
        if (!confirm) return;
        try
        {
            await _apiService.DeleteJobAsync(Job.Id);
            await Shell.Current.GoToAsync("..");
        }
        catch (Exception ex)
        {
            await Shell.Current.DisplayAlert("Error", ex.Message, "OK");
        }
    }

    [RelayCommand]
    private async Task AddNoteAsync()
    {
        if (string.IsNullOrWhiteSpace(NewNoteText) || Job == null) return;
        try
        {
            await _apiService.CreateJobNoteAsync(Job.Id, new CreateJobNoteRequest { Content = NewNoteText.Trim() });
            NewNoteText = string.Empty;
            await LoadJobAsync();
        }
        catch (Exception ex)
        {
            await Shell.Current.DisplayAlert("Error", ex.Message, "OK");
        }
    }

    [RelayCommand]
    private async Task DeleteNoteAsync(JobNoteResponse note)
    {
        if (Job == null) return;
        bool confirm = await Shell.Current.DisplayAlert("Delete Note", "Delete this note?", "Delete", "Cancel");
        if (!confirm) return;
        await _apiService.DeleteJobNoteAsync(Job.Id, note.Id);
        await LoadJobAsync();
    }

    [RelayCommand]
    private async Task PickPhotoAsync()
    {
        if (Job == null) return;
        var photo = await _photoService.PickAndUploadJobPhotoAsync(Job.Id);
        if (photo != null) await LoadJobAsync();
    }

    [RelayCommand]
    private async Task TakePhotoAsync()
    {
        if (Job == null) return;
        var photo = await _photoService.CaptureAndUploadJobPhotoAsync(Job.Id);
        if (photo != null) await LoadJobAsync();
    }

    [RelayCommand]
    private async Task DeletePhotoAsync(PhotoResponse photo)
    {
        bool confirm = await Shell.Current.DisplayAlert("Delete Photo", "Delete this photo?", "Delete", "Cancel");
        if (!confirm) return;
        await _apiService.DeletePhotoAsync(photo.Id);
        await LoadJobAsync();
    }

    [RelayCommand]
    private async Task ViewPhotosAsync(PhotoResponse photo)
    {
        if (Job?.Photos == null) return;
        var index = Job.Photos.IndexOf(photo);
        await Shell.Current.GoToAsync($"photoViewer?entityType=job&entityId={JobId}&startIndex={index}");
    }

    [RelayCommand]
    private async Task ViewCustomerAsync()
    {
        if (Job == null) return;
        await Shell.Current.GoToAsync($"customerDetail?id={Job.CustomerId}");
    }

    [RelayCommand]
    private async Task DeleteItemAsync(JobItemResponse item)
    {
        if (Job == null) return;
        bool confirm = await Shell.Current.DisplayAlert("Remove Item", $"Remove {item.Name}?", "Remove", "Cancel");
        if (!confirm) return;
        if (item.Source == "library")
            await _apiService.DeleteJobItemAsync(Job.Id, item.Id);
        else
            await _apiService.DeleteJobAdhocItemAsync(Job.Id, item.Id);
        await LoadJobAsync();
    }
}
