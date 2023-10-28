using RosyCrow.Services.Document;

namespace RosyCrow.Views;

public partial class WhatsNewPage : ContentPage
{
    private readonly IDocumentService _documentService;

    public WhatsNewPage(IDocumentService documentService)
    {
        _documentService = documentService;

        InitializeComponent();
    }

    private async void WhatsNewPage_OnAppearing(object sender, EventArgs e)
    {
        var html = await _documentService.RenderInternalDocument(@"whats-new");
        Browser.Source = new HtmlWebViewSource { Html = html };
    }

    private async void Browser_OnNavigating(object sender, WebNavigatingEventArgs e)
    {
        await Launcher.Default.OpenAsync(new Uri(e.Url));
    }
}