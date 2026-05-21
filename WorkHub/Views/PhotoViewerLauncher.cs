using CommunityToolkit.Maui.Views;
using Microsoft.Extensions.DependencyInjection;
using WorkHub.ViewModels;

namespace WorkHub.Views;

public static class PhotoViewerLauncher
{
    public static async Task ShowAsync(string entityType, string entityId, int startIndex)
    {
        var page = Application.Current?.Windows.FirstOrDefault()?.Page;
        if (page == null) return;

        var popup = MauiProgram.Services.GetRequiredService<PhotoViewerPopup>();
        if (popup.BindingContext is PhotoViewerViewModel vm)
            await vm.InitializeAsync(entityType, entityId, startIndex);

        await page.ShowPopupAsync(popup);
    }
}
