using System.Collections.ObjectModel;
using CommunityToolkit.Maui.Views;
using Microsoft.Extensions.DependencyInjection;
using WorkHub.ViewModels;

namespace WorkHub.Views;

public static class PhotoViewerLauncher
{
    // The viewer shares the caller's photo collection — no re-fetch, opens
    // instantly, and deletes made in the viewer update the caller's strip live.
    public static async Task ShowAsync(string title, ObservableCollection<PhotoDisplayModel> photos, int startIndex)
    {
        var page = Application.Current?.Windows.FirstOrDefault()?.Page;
        if (page == null) return;

        var popup = MauiProgram.Services.GetRequiredService<PhotoViewerPopup>();
        if (popup.BindingContext is PhotoViewerViewModel vm)
            vm.Initialize(title, photos, startIndex);

        await page.ShowPopupAsync(popup);
    }
}
