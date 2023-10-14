using Android.Content.Res;
using Microsoft.Extensions.Logging;
using RosyCrow.Extensions;
using RosyCrow.Services.Document;
using RosyCrow.Views;

// ReSharper disable AsyncVoidLambda

namespace RosyCrow;

public partial class App : Application
{
    private readonly ILogger<App> _logger;

    public App()
    {
        InitializeComponent();

        _logger = MauiProgram.Services.GetRequiredService<ILogger<App>>();
        MainPage = new NavigationPage(MauiProgram.Services.GetRequiredService<MainPage>());
    }

    protected override Window CreateWindow(IActivationState activationState)
    {
        var window = base.CreateWindow(activationState);

        window.Created += async (_, _) => await MauiProgram.Services.GetRequiredService<IDocumentService>().LoadResources();

        return window;
    }

    public void HandleNavigationIntent(string uri)
    {
        try
        {
            _logger.LogInformation("Handling intent to navigate to {URL}", uri);

            if (!string.IsNullOrWhiteSpace(uri) && (MainPage as NavigationPage)?.RootPage is MainPage page)
            {
                var alreadyOpenTab = page.Tabs.Tabs
                    .FirstOrDefault(t => uri.AreGeminiUrlsEqual(t.Url));

                // if this URL is already open in some tab, just select that tab.
                // otherwise, add a new tab
                if (alreadyOpenTab != null)
                    page.Tabs.SelectTab(alreadyOpenTab);
                else
                    Dispatcher.Dispatch(async () => await page.Tabs.AddTab(uri.ToGeminiUri()));
            }
        }
        catch (Exception e)
        {
            _logger.LogError(e, "Could not handle intent for {URL}", uri);
        }
    }
}