using CommunityToolkit.Maui.Core;
using Microsoft.Extensions.Logging;
using Newtonsoft.Json;
using RosyCrow.Extensions;
using RosyCrow.Models.Serialization;
using RosyCrow.Resources.Localization;
using RosyCrow.Views;
using Tab = RosyCrow.Models.Tab;

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

    public async Task HandleImportTabsIntent(Stream stream)
    {
        try
        {
            if ((MainPage as NavigationPage)?.RootPage is not MainPage page)
                return;

            using var reader = new StreamReader(stream);

            try
            {
                var tabs = JsonConvert.DeserializeObject<SerializedTab[]>(await reader.ReadToEndAsync());

                if (tabs?.Any() ?? false)
                {
                    if (!await page.DisplayAlertOnMainThread(Text.App_HandleImportTabsIntent_Import_Tabs,
                            string.Format(Text.App_HandleImportTabsIntent_Do_you_want_to_import__0__tabs_, tabs.Length),
                            Text.App_HandleImportTabsIntent_Yes,
                            Text.App_HandleImportTabsIntent_No))
                        return;

                    await page.TabCollection.ImportTabs(tabs.Select(t => new Tab
                    {
                        Url = t.Url,
                        Label = t.Icon
                    }));
                }
                else
                {
                    page.ShowToast(Text.App_HandleImportTabsIntent_The_file_contains_no_tabs, ToastDuration.Short);
                }
            }
            catch (JsonException)
            {
                page.ShowToast(Text.App_HandleImportTabsIntent_The_file_is_formatted_incorrectly, ToastDuration.Long);
                throw;
            }
            catch (Exception)
            {
                page.ShowToast(Text.App_HandleImportTabsIntent_No_tabs_were_imported_because_something_went_wrong, ToastDuration.Long);
                throw;
            }
        }
        catch (Exception e)
        {
            _logger.LogError(e, @"Could not handle tab import intent");
        }
        finally
        {
            stream?.Close();
        }
    }

    public void HandleNavigationIntent(string uri)
    {
        try
        {
            _logger.LogInformation(@"Handling intent to navigate to {URL}", uri);

            if (!string.IsNullOrWhiteSpace(uri) && MainPage is NavigationPage { RootPage: MainPage page })
            {
                try
                {
                    var alreadyOpenTab = page.TabCollection.Tabs
                        .FirstOrDefault(t => uri.AreGeminiUrlsEqual(t.Url));

                    // if this URL is already open in some tab, just select that tab.
                    // otherwise, add a new tab
                    if (alreadyOpenTab != null)
                        page.TabCollection.SelectTab(alreadyOpenTab);
                    else
                        Dispatcher.Dispatch(async () => await page.TabCollection.AddTab(uri.ToGeminiUri()));
                }
                catch (Exception)
                {
                    page.ShowToast(Text.App_HandleNavigationIntent_Something_went_wrong__so_the_link_could_not_be_opened, ToastDuration.Long);
                    throw;
                }
            }
        }
        catch (Exception e)
        {
            _logger.LogError(e, @"Could not handle navigation intent for {URL}", uri);
        }
    }
}