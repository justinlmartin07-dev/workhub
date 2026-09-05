using Android.OS;
using Android.Print;
using Android.Runtime;

namespace WorkHub;

// Renders a WebView's content to a paginated PDF file by driving the
// framework's print pipeline directly (the same adapter PrintManager uses,
// minus the print dialog). The framework's LayoutResultCallback and
// WriteResultCallback have package-private Java constructors, so the
// subclasses below construct their Java peers by hand through JNIEnv and
// register their Java wrappers inside the android.print package (so the
// wrapper can reach the package-private super constructor) — the
// long-standing Xamarin/MAUI workaround for WebView-to-PDF export.
public static class WebViewPdfExporter
{
    public static Task<string> ExportAsync(Android.Webkit.WebView webView, string documentName, string outputPath)
    {
        var tcs = new TaskCompletionSource<string>();
        var adapter = webView.CreatePrintDocumentAdapter(documentName);
        var attributes = new PrintAttributes.Builder()
            .SetMediaSize(PrintAttributes.MediaSize.NaLetter!)
            .SetResolution(new PrintAttributes.Resolution("pdf", "pdf", 300, 300))
            .SetMinMargins(PrintAttributes.Margins.NoMargins!)
            .Build();

        adapter.OnLayout(null, attributes, null, new PdfLayoutCallback(
            onFinished: () =>
            {
                try
                {
                    var fd = ParcelFileDescriptor.Open(new Java.IO.File(outputPath),
                        ParcelFileMode.Create | ParcelFileMode.Truncate | ParcelFileMode.ReadWrite)!;
                    adapter.OnWrite([PageRange.AllPages!], fd, null, new PdfWriteCallback(error =>
                    {
                        fd.Close();
                        if (error == null) tcs.TrySetResult(outputPath);
                        else tcs.TrySetException(new Exception(error));
                    }));
                }
                catch (Exception ex)
                {
                    tcs.TrySetException(ex);
                }
            },
            onFailed: error => tcs.TrySetException(new Exception(error))), new Bundle());

        return tcs.Task;
    }
}

[Register("android/print/WorkHubPdfLayoutCallback")]
internal class PdfLayoutCallback : PrintDocumentAdapter.LayoutResultCallback
{
    private readonly Action _onFinished;
    private readonly Action<string> _onFailed;

    public PdfLayoutCallback(Action onFinished, Action<string> onFailed)
        : base(IntPtr.Zero, JniHandleOwnership.DoNotTransfer)
    {
        if (Handle == IntPtr.Zero)
        {
            SetHandle(JNIEnv.StartCreateInstance(GetType(), "()V"), JniHandleOwnership.TransferLocalRef);
            JNIEnv.FinishCreateInstance(Handle, "()V");
        }
        _onFinished = onFinished;
        _onFailed = onFailed;
    }

    public override void OnLayoutFinished(PrintDocumentInfo? info, bool changed) => _onFinished();

    public override void OnLayoutFailed(Java.Lang.ICharSequence? error)
        => _onFailed(error?.ToString() ?? "PDF layout failed.");

    public override void OnLayoutCancelled() => _onFailed("PDF layout was cancelled.");
}

// Reports null on success, an error message otherwise.
[Register("android/print/WorkHubPdfWriteCallback")]
internal class PdfWriteCallback : PrintDocumentAdapter.WriteResultCallback
{
    private readonly Action<string?> _onDone;

    public PdfWriteCallback(Action<string?> onDone)
        : base(IntPtr.Zero, JniHandleOwnership.DoNotTransfer)
    {
        if (Handle == IntPtr.Zero)
        {
            SetHandle(JNIEnv.StartCreateInstance(GetType(), "()V"), JniHandleOwnership.TransferLocalRef);
            JNIEnv.FinishCreateInstance(Handle, "()V");
        }
        _onDone = onDone;
    }

    public override void OnWriteFinished(PageRange[]? pages) => _onDone(null);

    public override void OnWriteFailed(Java.Lang.ICharSequence? error)
        => _onDone(error?.ToString() ?? "PDF export failed.");

    public override void OnWriteCancelled() => _onDone("PDF export was cancelled.");
}
