using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using WorkHub.Models;
using WorkHub.Services;

namespace WorkHub.ViewModels;

public partial class ChangePasswordViewModel : BaseViewModel
{
    private readonly ApiService _apiService;

    [ObservableProperty]
    private string _currentPassword = string.Empty;

    [ObservableProperty]
    private string _newPassword = string.Empty;

    [ObservableProperty]
    private string _confirmPassword = string.Empty;

    [ObservableProperty]
    private string? _successMessage;

    public ChangePasswordViewModel(ApiService apiService)
    {
        _apiService = apiService;
    }

    [RelayCommand]
    private async Task ChangePasswordAsync()
    {
        if (string.IsNullOrWhiteSpace(CurrentPassword) || string.IsNullOrWhiteSpace(NewPassword))
        {
            ErrorMessage = "All fields are required";
            HasError = true;
            return;
        }

        if (NewPassword.Length < 8)
        {
            ErrorMessage = "New password must be at least 8 characters";
            HasError = true;
            return;
        }

        if (NewPassword != ConfirmPassword)
        {
            ErrorMessage = "Passwords do not match";
            HasError = true;
            return;
        }

        await LoadAsync(async () =>
        {
            var request = new ChangePasswordRequest
            {
                CurrentPassword = CurrentPassword,
                NewPassword = NewPassword
            };
            await _apiService.ChangePasswordAsync(request);
            SuccessMessage = "Password changed successfully";
            CurrentPassword = string.Empty;
            NewPassword = string.Empty;
            ConfirmPassword = string.Empty;
        });
    }
}
