using CommunityToolkit.Maui.Views;
using WorkHub.ViewModels;

namespace WorkHub.Views;

public partial class PhotoViewerPopup : Popup
{
    private readonly PhotoViewerViewModel _viewModel;

    public PhotoViewerPopup(PhotoViewerViewModel viewModel)
    {
        InitializeComponent();
        _viewModel = viewModel;
        BindingContext = viewModel;
        viewModel.CloseRequested += OnCloseRequested;

        var info = DeviceDisplay.Current.MainDisplayInfo;
        var density = info.Density > 0 ? info.Density : 1.0;
        var widthDp = info.Width / density;
        var heightDp = info.Height / density;
        var w = Math.Min(widthDp * 0.92, 760);
        var h = Math.Min(heightDp * 0.88, 880);
        Size = new Size(w, h);
    }

    private void OnCloseRequested() => Dispatcher.Dispatch(() => Close());

    private async void OnEllipsisClicked(object? sender, EventArgs e)
    {
        var page = Application.Current?.Windows.FirstOrDefault()?.Page;
        if (page == null) return;

        var choice = await page.DisplayActionSheet("Photo", "Cancel", null, "Save", "Share", "Delete");
        switch (choice)
        {
            case "Save":
                await _viewModel.SaveCurrentPhotoCommand.ExecuteAsync(null);
                break;
            case "Share":
                await _viewModel.ShareCurrentPhotoCommand.ExecuteAsync(null);
                break;
            case "Delete":
                await _viewModel.DeleteCurrentPhotoCommand.ExecuteAsync(null);
                break;
        }
    }
}
