using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using WorkHub.Services;

namespace WorkHub.ViewModels;

public partial class MainLayoutViewModel : BaseViewModel
{
    private readonly AuthService _authService;

    [ObservableProperty]
    private int _selectedTabIndex;

    [ObservableProperty]
    private string _userName = string.Empty;

    [ObservableProperty]
    private string _userInitial = "?";

    public MainLayoutViewModel(AuthService authService)
    {
        _authService = authService;
        var user = _authService.CurrentUser;
        if (user != null)
        {
            UserName = user.Name;
            UserInitial = user.Name.Length > 0 ? user.Name[0].ToString().ToUpper() : "?";
        }
    }

    [RelayCommand]
    private void SelectTab(string tabIndex)
    {
        if (int.TryParse(tabIndex, out var index))
            SelectedTabIndex = index;
    }

    [RelayCommand]
    private async Task GoToProfileAsync()
    {
        await Shell.Current.GoToAsync("profile");
    }

    [RelayCommand]
    private async Task LogoutAsync()
    {
        await _authService.LogoutAsync();
        await Shell.Current.GoToAsync("../login");
    }
}
