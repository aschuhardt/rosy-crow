using Android.Content;
using Android.Print;
using Yarrow.Interfaces;
using WebView = Android.Webkit.WebView;

namespace Yarrow.Platforms.Android;

public class AndroidPrintService : IPrintService
{
    private readonly WebView _webView;

    public AndroidPrintService(WebView webView)
    {
        _webView = webView;
    }

    public void Print(string name)
    {
        name ??= $"page_{DateTimeOffset.UtcNow.ToUnixTimeSeconds()}";
        var printManager = (PrintManager)Platform.CurrentActivity?.GetSystemService(Context.PrintService);
        printManager?.Print(name, _webView.CreatePrintDocumentAdapter(name), null);
    }
}