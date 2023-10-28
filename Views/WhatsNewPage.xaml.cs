using RosyCrow.Models;
using RosyCrow.Services.Document;

namespace RosyCrow.Views;

public partial class WhatsNewPage : ContentPage
{
    private readonly IDocumentService _documentService;
    private readonly MainPage _mainPage;

    public WhatsNewPage(IDocumentService documentService, MainPage mainPage)
    {
        _documentService = documentService;
        _mainPage = mainPage;

        InitializeComponent();
    }

    private async void WhatsNewPage_OnAppearing(object sender, EventArgs e)
    {
        var html = await _documentService.RenderInternalDocument(@"whats-new");
        Browser.Source = new HtmlWebViewSource { Html = html };
    }

    private async void Browser_OnNavigating(object sender, WebNavigatingEventArgs e)
    {
        if (string.IsNullOrEmpty(e.Url))
            return;

        if (new Uri(e.Url) is { Scheme: Constants.GeminiScheme } url)
        {
            // we don't want to refresh the current tab, we want to load the new one
            _mainPage.LoadPageOnAppearing = false;

            await _mainPage.TabCollection.AddTab(url);
            await Navigation.PopToRootAsync(true);
        }
        else
        {
            await Launcher.Default.TryOpenAsync(e.Url);
        }
    }
}