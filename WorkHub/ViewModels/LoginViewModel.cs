using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using WorkHub.Services;

namespace WorkHub.ViewModels;

public partial class LoginViewModel : BaseViewModel
{
    private readonly AuthService _authService;

    [ObservableProperty]
    private string _email = string.Empty;

    [ObservableProperty]
    private string _password = string.Empty;

    public LoginViewModel(AuthService authService)
    {
        _authService = authService;
    }

    [RelayCommand]
    private async Task LoginAsync()
    {
        if (string.IsNullOrWhiteSpace(Email) || string.IsNullOrWhiteSpace(Password))
        {
            ErrorMessage = "Email and password are required";
            HasError = true;
            return;
        }

        HasError = false;
        ErrorMessage = null;
        IsBusy = true;
        IsLoading = true;

        try
        {
            var (success, error) = await _authService.LoginAsync(Email, Password);
            if (!success)
            {
                ErrorMessage = error ?? "Login failed";
                HasError = true;
                return;
            }
        }
        catch (Exception ex)
        {
            ErrorMessage = ex.Message;
            HasError = true;
            return;
        }
        finally
        {
            IsBusy = false;
            IsLoading = false;
        }

        try
        {
            await Shell.Current.GoToAsync("../main");
        }
        catch (Exception ex)
        {
            var msg = $"Navigation error: {ex.Message}\n{ex.InnerException?.Message}";
            ErrorMessage = msg;
            HasError = true;
            System.Diagnostics.Debug.WriteLine(msg);
            // Also write to a file we can check
            try
            {
                var logPath = Path.Combine(FileSystem.AppDataDirectory, "crash.log");
                await File.WriteAllTextAsync(logPath, $"{DateTime.Now}\n{ex}");
            }
            catch { }
        }
    }
}
