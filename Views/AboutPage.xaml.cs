using RosyCrow.Services.Document;

namespace RosyCrow.Views;

public partial class AboutPage : ContentPage
{
    private readonly IDocumentService _documentService;

    public AboutPage(IDocumentService documentService)
    {
        _documentService = documentService;
        InitializeComponent();
    }

    private async void AboutPage_OnAppearing(object sender, EventArgs e)
    {
        var html = await _documentService.RenderInternalDocument(@"about");
        Browser.Source = new HtmlWebViewSource { Html = html };
    }

    private void Browser_OnNavigating(object sender, WebNavigatingEventArgs e)
    {
        var uri = new Uri(e.Url);
        Launcher.Default.OpenAsync(uri);
    }
}