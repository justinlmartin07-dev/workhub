using CommunityToolkit.Mvvm.ComponentModel;

namespace WorkHub.ViewModels;

public partial class BaseViewModel : ObservableObject
{
    [ObservableProperty]
    private bool _isLoading;

    [ObservableProperty]
    private bool _isBusy;

    [ObservableProperty]
    private string? _errorMessage;

    [ObservableProperty]
    private bool _hasError;

    [ObservableProperty]
    private bool _hasContent;

    [ObservableProperty]
    private bool _isEmpty;

    protected async Task LoadAsync(Func<Task> action, bool showLoading = true)
    {
        if (IsBusy) return;

        try
        {
            IsBusy = true;
            if (showLoading) IsLoading = true;
            HasError = false;
            ErrorMessage = null;

            await action();

            HasContent = true;
            IsEmpty = false;
        }
        catch (HttpRequestException)
        {
            ErrorMessage = "Unable to connect to server";
            HasError = true;
            HasContent = false;
        }
        catch (Exception ex)
        {
            ErrorMessage = $"{ex.GetType().Name}: {ex.Message}";
            HasError = true;
            HasContent = false;
            var path = Path.Combine(FileSystem.AppDataDirectory, "crash.log");
            File.WriteAllText(path, $"{DateTime.Now}\n{ex}\n");
        }
        finally
        {
            IsBusy = false;
            IsLoading = false;
        }
    }

    protected void SetEmpty()
    {
        IsEmpty = true;
        HasContent = false;
    }

    protected void SetContent()
    {
        IsEmpty = false;
        HasContent = true;
    }
}