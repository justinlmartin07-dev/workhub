using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using WorkHub.Models;
using WorkHub.Services;

namespace WorkHub.ViewModels;

public partial class ProfileViewModel : BaseViewModel
{
    private readonly ApiService _apiService;
    private readonly AuthService _authService;
    private readonly PhotoService _photoService;

    [ObservableProperty]
    private UserProfileResponse? _profile;

    [ObservableProperty]
    private string _name = string.Empty;

    [ObservableProperty]
    private bool _isEditing;

    public ProfileViewModel(ApiService apiService, AuthService authService, PhotoService photoService)
    {
        _apiService = apiService;
        _authService = authService;
        _photoService = photoService;
    }

    [RelayCommand]
    public async Task LoadProfileAsync()
    {
        await LoadAsync(async () =>
        {
            Profile = await _apiService.GetProfileAsync();
            if (Profile != null)
                Name = Profile.Name;
        });
    }

    [RelayCommand]
    private void ToggleEdit()
    {
        IsEditing = !IsEditing;
        if (!IsEditing && Profile != null)
            Name = Profile.Name;
    }

    [RelayCommand]
    private async Task SaveProfileAsync()
    {
        if (string.IsNullOrWhiteSpace(Name))
        {
            ErrorMessage = "Name is required";
            HasError = true;
            return;
        }

        await LoadAsync(async () =>
        {
            var request = new UpdateProfileRequest { Name = Name.Trim() };
            Profile = await _apiService.UpdateProfileAsync(request);
            IsEditing = false;
        });
    }

    [RelayCommand]
    private async Task ChangePhotoAsync()
    {
        var photo = await _photoService.PickAndUploadProfilePhotoAsync();
        if (photo != null) await LoadProfileAsync();
    }

    [RelayCommand]
    private async Task DeletePhotoAsync()
    {
        bool confirm = await Shell.Current.DisplayAlert("Remove Photo", "Remove your profile photo?", "Remove", "Cancel");
        if (!confirm) return;
        await _apiService.DeleteProfilePhotoAsync();
        await LoadProfileAsync();
    }

    [RelayCommand]
    private async Task ChangePasswordAsync()
    {
        await Shell.Current.GoToAsync("changePassword");
    }

    [RelayCommand]
    private async Task LogoutAsync()
    {
        bool confirm = await Shell.Current.DisplayAlert("Logout", "Are you sure you want to logout?", "Logout", "Cancel");
        if (!confirm) return;
        await _authService.LogoutAsync();
        await Shell.Current.GoToAsync("../login");
    }
}
