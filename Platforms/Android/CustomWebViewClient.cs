using Android.Webkit;
using Opal;
using Opal.Response;
using WebView = Android.Webkit.WebView;

namespace RosyCrow.Platforms.Android;

internal class CustomWebViewClient : WebViewClient
{
    private readonly IOpalClient _geminiClient;

    public CustomWebViewClient(IOpalClient geminiClient)
    {
        _geminiClient = geminiClient;
    }

    public override WebResourceResponse ShouldInterceptRequest(WebView view, IWebResourceRequest request)
    {
        if (request.IsForMainFrame || request.Url?.Scheme != "gemini")
            return base.ShouldInterceptRequest(view, request);

        for (var i = 0; i < 3; i++)
        {
            var response = _geminiClient.SendRequestAsync(request.Url.ToString()).GetAwaiter().GetResult();
            if (response is SuccessfulResponse success)
                return new WebResourceResponse(success.MimeType, "binary", success.Body);
        }

        return new WebResourceResponse(null, null, Stream.Null);
    }
}