namespace WorkHub.Views;

// Modal preview for printable summaries. Shows the HTML document in a WebView.
// Print hands off to the platform print pipeline: WebView2's print UI on
// Windows, PrintManager (system print preview / save-as-PDF) on Android.
// Share asks for a format and opens the OS share sheet: a PDF export (best
// for email) or a plain-text rendering of the same data (best for texting —
// readable in any messaging app, no MMS image compression).
public partial class PrintPreviewPage : ContentPage
{
    private readonly string _documentName;
    private readonly string _plainText;

    public PrintPreviewPage(string html, string plainText, string documentName)
    {
        InitializeComponent();
        _documentName = documentName;
        _plainText = plainText;
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

    private async void OnShareClicked(object? sender, EventArgs e)
    {
        try
        {
            var choice = await DisplayActionSheet($"Share {_documentName}", "Cancel", null,
                "PDF file", "Plain text");
            if (choice == "PDF file")
                await SharePdfAsync();
            else if (choice == "Plain text")
                await ShareTextAsync();
        }
        catch (Exception ex)
        {
            await DisplayAlert("Share Error", ex.Message, "OK");
        }
    }

    private async Task SharePdfAsync()
    {
        var pdfPath = await ExportPdfAsync();
        if (pdfPath == null) return;

        try
        {
            await Share.Default.RequestAsync(new ShareFileRequest
            {
                Title = _documentName,
                File = new ShareFile(pdfPath),
            });
        }
        catch
        {
#if WINDOWS
            // The share sheet can be unavailable on unpackaged WinUI; opening
            // the exported PDF still lets the user attach it from their mail app.
            await Launcher.Default.OpenAsync(new OpenFileRequest(_documentName, new ReadOnlyFile(pdfPath)));
#else
            throw;
#endif
        }
    }

    private async Task ShareTextAsync()
    {
        try
        {
            await Share.Default.RequestAsync(new ShareTextRequest
            {
                Title = _documentName,
                Text = _plainText,
            });
        }
        catch
        {
#if WINDOWS
            // Same unpackaged-WinUI caveat as above; the clipboard keeps the
            // text usable in any messaging app.
            await Clipboard.Default.SetTextAsync(_plainText);
            await DisplayAlert("Copied", "Sharing isn't available, so the summary was copied to the clipboard instead.", "OK");
#else
            throw;
#endif
        }
    }

    // Exports the previewed document to a PDF in the cache directory and
    // returns its path, or null when export isn't supported on this platform.
    private async Task<string?> ExportPdfAsync()
    {
        var fileName = string.Join("_", _documentName.Split(Path.GetInvalidFileNameChars())) + ".pdf";
        var pdfPath = Path.Combine(FileSystem.CacheDirectory, fileName);
        if (File.Exists(pdfPath))
            File.Delete(pdfPath);

#if WINDOWS
        if (PreviewWebView.Handler?.PlatformView is Microsoft.UI.Xaml.Controls.WebView2 webView)
        {
            await webView.EnsureCoreWebView2Async();
            if (!await webView.CoreWebView2.PrintToPdfAsync(pdfPath, null))
                throw new Exception("The document could not be exported to PDF.");
            return pdfPath;
        }
        return null;
#elif ANDROID
        if (PreviewWebView.Handler?.PlatformView is Android.Webkit.WebView webView)
            return await WebViewPdfExporter.ExportAsync(webView, _documentName, pdfPath);
        return null;
#else
        await Task.CompletedTask;
        return null;
#endif
    }
}
