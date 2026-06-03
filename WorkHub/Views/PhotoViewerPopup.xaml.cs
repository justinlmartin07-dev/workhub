using CommunityToolkit.Maui.Views;
using WorkHub.ViewModels;

namespace WorkHub.Views;

public partial class PhotoViewerPopup : Popup
{
    // 1/4" ≈ 24dp at 96dpi
    private const double WidePadding = 24;
    private const double NarrowBreakpointDp = 720;

    public Thickness PhotoAreaPadding { get; }

    public PhotoViewerPopup(PhotoViewerViewModel viewModel)
    {
        var info = DeviceDisplay.Current.MainDisplayInfo;
        var density = info.Density > 0 ? info.Density : 1.0;
        var widthDp = info.Width / density;
        PhotoAreaPadding = widthDp >= NarrowBreakpointDp
            ? new Thickness(WidePadding)
            : new Thickness(0, WidePadding, 0, WidePadding);

        InitializeComponent();
        BindingContext = viewModel;
        viewModel.CloseRequested += OnCloseRequested;

#if WINDOWS
        TopSpacerRow.Height = new GridLength(32);
#endif
    }

    private void OnCloseRequested() => Dispatcher.Dispatch(() => Close());
}
