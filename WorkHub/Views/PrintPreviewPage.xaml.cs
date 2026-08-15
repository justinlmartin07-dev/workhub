namespace WorkHub.Views;

// Modal preview for printable summaries. Shows the HTML document in a WebView
// and hands off to the platform print pipeline: WebView2's print UI on Windows,
// PrintManager (system print preview / save-as-PDF) on Android.
public partial class PrintPreviewPage : ContentPage
{
    private readonly string _documentName;

    public PrintPreviewPage(string html, string documentName)
    {
        InitializeComponent();
        _documentName = documentName;
        TitleLabel.Text = documentName;
        PreviewWebView.Source = new HtmlWebViewSource { Html = html };
    }

    private async void OnCloseClicked(object? sender, EventArgs e)
        => await Navigation.PopModalAsync();

    private async void OnPrintClicked(object? sender, EventArgs e)
    {
        try
        {
#if WINDOWS
            if (PreviewWebView.Handler?.PlatformView is Microsoft.UI.Xaml.Controls.WebView2 webView)
            {
                await webView.EnsureCoreWebView2Async();
                webView.CoreWebView2.ShowPrintUI(Microsoft.Web.WebView2.Core.CoreWebView2PrintDialogKind.Browser);
            }
#elif ANDROID
            if (PreviewWebView.Handler?.PlatformView is Android.Webkit.WebView webView &&
                Platform.CurrentActivity?.GetSystemService(Android.Content.Context.PrintService)
                    is Android.Print.PrintManager printManager)
            {
                var adapter = webView.CreatePrintDocumentAdapter(_documentName);
                printManager.Print(_documentName, adapter, new Android.Print.PrintAttributes.Builder().Build());
            }
            await Task.CompletedTask;
#else
            await Task.CompletedTask;
#endif
        }
        catch (Exception ex)
        {
            await DisplayAlert("Print Error", ex.Message, "OK");
        }
    }
}
