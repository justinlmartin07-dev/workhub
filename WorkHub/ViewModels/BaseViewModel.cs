using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

namespace WorkHub.ViewModels;

public interface IHasUnsavedChanges
{
    bool HasUnsavedChanges { get; }
}

// Detail VMs whose view is cached and reused by MainLayout. Called when the
// same item is shown again (the entity-id property didn't change, so the
// property setter won't trigger a reload itself).
public interface IReusableDetail
{
    void RefreshOnReuse();
}

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

    // Bound (TwoWay) by the Android pull-to-refresh RefreshView.
    [ObservableProperty]
    private bool _isRefreshing;

    // Single entry point for manual refresh (F5 on Windows, pull-to-refresh on
    // Android). Always clears IsRefreshing so the pull spinner retracts even
    // when LoadAsync skipped the reload because a load was already in flight.
    [RelayCommand]
    private async Task RefreshAsync()
    {
        try { await OnRefreshRequestedAsync(); }
        finally { IsRefreshing = false; }
    }

    protected virtual Task OnRefreshRequestedAsync() => Task.CompletedTask;

    protected async Task LoadAsync(Func<Task> action, bool showLoading = true)
    {
        if (IsBusy) return;

        // A refresh of already-visible content shouldn't blank the page on failure —
        // keep showing the (stale) data instead of swapping in an error state.
        var keepContentOnError = !showLoading && HasContent;

        try
        {
            IsBusy = true;
            if (showLoading) IsLoading = true;
            HasError = false;
            ErrorMessage = null;

            await action();

            // The action may have called SetEmpty/SetContent; only apply the
            // default "has content" outcome when it didn't decide a state itself.
            if (!IsEmpty && !HasContent) SetContent();
        }
        catch (HttpRequestException ex)
        {
            if (!keepContentOnError)
            {
                ErrorMessage = ex.StatusCode.HasValue
                    ? $"Server error ({(int)ex.StatusCode}): {ex.Message}"
                    : "Unable to connect to server";
                HasError = true;
                HasContent = false;
            }
        }
        catch (Exception ex)
        {
            if (!keepContentOnError)
            {
                ErrorMessage = $"{ex.GetType().Name}: {ex.Message}";
                HasError = true;
                HasContent = false;
            }
#if DEBUG
            var path = Path.Combine(FileSystem.AppDataDirectory, "crash.log");
            File.WriteAllText(path, $"{DateTime.Now}\n{ex}\n");
#endif
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