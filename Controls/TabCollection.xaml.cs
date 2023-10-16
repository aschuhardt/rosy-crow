using System.Collections.ObjectModel;
using System.Globalization;
using System.Text;
using CommunityToolkit.Maui.Alerts;
using Microsoft.Extensions.Logging;
using Opal;
using Opal.Response;
using Opal.Tofu;
using RosyCrow.Controls.Tabs;
using RosyCrow.Extensions;
using RosyCrow.Interfaces;
using RosyCrow.Models;
using RosyCrow.Views;
using Tab = RosyCrow.Models.Tab;

namespace RosyCrow.Controls;

public partial class TabCollection : ContentView
{
    private readonly IBrowsingDatabase _browsingDatabase;
    private readonly ILogger<TabCollection> _logger;

    private Tab _selectedTab;
    private BrowserView _selectedView;
    private ObservableCollection<Tab> _tabs;

    public TabCollection()
        : this(
            MauiProgram.Services.GetRequiredService<IBrowsingDatabase>(),
            MauiProgram.Services.GetRequiredService<ILogger<TabCollection>>())
    {
    }

    public TabCollection(IBrowsingDatabase browsingDatabase, ILogger<TabCollection> logger)
    {
        _browsingDatabase = browsingDatabase;
        _logger = logger;

        InitializeComponent();

        BindingContext = this;

        Tabs = _browsingDatabase.Tabs;
    }

    public BrowserView SelectedView
    {
        get => _selectedView;
        set
        {
            if (Equals(value, _selectedView)) return;

            _selectedView = value;
            SelectedViewChanged?.Invoke(this, EventArgs.Empty);
            OnPropertyChanged();
        }
    }

    public ObservableCollection<Tab> Tabs
    {
        get => _tabs;
        set
        {
            if (Equals(value, _tabs))
                return;

            _tabs = value;
            OnPropertyChanged();
        }
    }

    public ContentPage ParentPage { get; set; }

    public event EventHandler SelectedViewChanged;

    public void SelectTab(Tab tab)
    {
        if (tab.View == null)
        {
            // the tab was just loaded from the database and hasn't been initialized yet
            InitializeTab(tab);

            // when the tab is ready to be shown, it will raise ReadyToShow which
            // will call this method again
            return;
        }

        SelectedView = tab.View;

        if (_selectedTab != null)
        {
            _selectedTab.Selected = false;
            _browsingDatabase.Update(_selectedTab);
        }

        _selectedTab = tab;
        tab.Selected = true;
        _browsingDatabase.Update(tab);
    }

    public Task AddDefaultTab()
    {
        return AddTab("rosy-crow://default", "🐦");
    }

    public Task AddTab(Uri uri)
    {
        return AddTab(uri.ToString(), uri.Host[..1]);
    }

    public Task AddTab(string url, string label)
    {
        _logger.LogDebug("Adding a new tab for {URL} with label {Label}", url, label);

        var tab = new Tab(url, label)
        {
            Selected = true
        };

        InitializeTab(tab);
        Tabs.Add(tab);

        return _browsingDatabase.UpdateTabOrder();
    }

    private void InitializeTab(Tab tab)
    {
        _logger.LogDebug("Initializing a new tab");
        tab.View = MauiProgram.Services.GetRequiredService<BrowserView>();
        tab.View.ParentPage = ParentPage;
        tab.View.ReadyToShow += (_, _) => SelectTab(tab);
        tab.View.Location = tab.Url.ToGeminiUri();
        tab.View.PageLoaded += (_, _) => UpdateTabWithPageInfo(tab);
        tab.View.OpeningUrlInNewTab += (_, arg) => AddTab(arg.Uri);
    }

    private void UpdateTabWithPageInfo(Tab tab, bool useDefaultLabel = false)
    {
        var uri = tab.View.Location;

        if (!useDefaultLabel && _browsingDatabase.TryGetCapsule(uri.Host, out var capsule) && !string.IsNullOrEmpty(capsule.Icon))
        {
            _logger.LogInformation("Capsule {Host} has a stored icon: {Icon}", uri.Host, capsule.Icon);
            tab.Label = capsule.Icon;
        }
        else
        {
            tab.Label = tab.View.PageTitle?[..1] ?? uri.Host[..1].ToUpperInvariant();
        }

        tab.Url = uri.ToString();
        _browsingDatabase.Update(tab);
    }

    private void BrowserTab_OnSelected(object sender, TabEventArgs e)
    {
        SelectTab(e.Tab);
    }

    private async void AdderTab_OnTriggered(object sender, EventArgs e)
    {
        await AddDefaultTab();
    }

    private async void BrowserTab_OnRemoveRequested(object sender, TabEventArgs e)
    {
        _logger.LogInformation("Removing tab {Url}", e.Tab.Url);

        Tabs.Remove(e.Tab);

        if (!Tabs.Any())
        {
            await AddDefaultTab();
        }
        else
        {
            // try to find the tab that is to the left of this one; if there is none, just select the first one
            var next = Tabs.OrderByDescending(t => t.Order).FirstOrDefault(t => t.Order < e.Tab.Order);
            SelectTab(next ?? _tabs.First());
        }
    }

    private async void TabsCollectionView_OnReorderCompleted(object sender, EventArgs e)
    {
        await _browsingDatabase.UpdateTabOrder();
    }

    private void TabCollection_OnLoaded(object sender, EventArgs e)
    {
        if (Tabs.FirstOrDefault(t => t.Selected) is { } tab)
            SelectTab(tab);
    }

    private async void BrowserTab_OnFetchingIcon(object sender, TabEventArgs e)
    {
        var host = e.Tab?.Url?.ToGeminiUri().Host;
        if (string.IsNullOrWhiteSpace(host))
            return;

        try
        {
            e.Tab.Label = "…";
            var label = await FetchFavicon(host);

            if (string.IsNullOrWhiteSpace(label))
            {
                await Toast.Make("No icon available").Show();
                return;
            }

            // ensure that there is only one text 'element' in the label
            var info = new StringInfo(label);
            if (info.LengthInTextElements > 0)
                label = info.SubstringByTextElements(0, 1);

            e.Tab.Label = label;

            _browsingDatabase.Update(e.Tab);

            if (_browsingDatabase.TryGetCapsule(host, out var capsule))
            {
                capsule.Icon = label;
                _browsingDatabase.Update(capsule);
            }
            else
            {
                _browsingDatabase.InsertOrReplace(new Capsule
                {
                    Host = host,
                    Icon = label
                });
            }

            await Toast.Make("Icon updated").Show();
        }
        catch (Exception ex)
        {
            await Toast.Make("Failed to fetch the icon due to an error").Show();
            _logger.LogError(ex, "Failed to fetch or set icon via favicon.txt for {Host}", host);
        }
    }

    public async Task<string> FetchFavicon(string host)
    {
        try
        {
            _logger.LogInformation("Attempting to fetch favicon.txt for {Host}", host);

            // do not follow redirects that the user isn't aware of
            var client = new OpalClient(new DummyCertificateDatabase(), RedirectBehavior.Ignore);
            var response = await client.SendRequestAsync($"gemini://{host}/favicon.txt");

            if (response is SuccessfulResponse success && success.Body.CanRead && success.MimeType.StartsWith("text/"))
            {
                var buffer = new byte[8];
                var size = await success.Body.ReadAsync(buffer, 0, buffer.Length);
                var label = Encoding.UTF8.GetString(buffer, 0, size);
                if (!string.IsNullOrWhiteSpace(label))
                    return label;
            }
            else if (response is ErrorResponse error)
            {
                _logger.LogInformation("Unsuccessful response to request for favicon.txt from {Host}: {Status} {Meta}",
                    error.Uri,
                    error.Status,
                    error.Message);
            }
        }
        catch (TaskCanceledException)
        {
            _logger.LogWarning("Timed-out waiting for a request for favicon from {Host}", host);
        }
        catch (Exception e)
        {
            _logger.LogError(e, "Failed to fetch favicon for {Host}", host);
            throw;
        }

        return null;
    }

    private async void BrowserTab_OnResettingIcon(object sender, TabEventArgs e)
    {
        // this feature will only be displayed in the context menu if
        // the tab has been initialized (so that we have a title to use) AND if the capsule has a saved icon
        var host = e.Tab?.View?.Location?.Host;
        if (string.IsNullOrWhiteSpace(host))
            return;

        try
        {
            if (_browsingDatabase.TryGetCapsule(host, out var capsule))
            {
                _logger.LogInformation("Clearing the stored icon for {Host}", capsule.Host);

                capsule.Icon = null;
                _browsingDatabase.Update(capsule);

                await Toast.Make("The icon has been reset").Show();
            }

            UpdateTabWithPageInfo(e.Tab, true);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to reset icon for {Host}", host);
            throw;
        }
    }

    private async void BrowserTab_OnSettingCustomIcon(object sender, TabEventArgs e)
    {
        var host = e.Tab?.Url?.ToGeminiUri().Host;
        if (string.IsNullOrWhiteSpace(host))
            return;

        _logger.LogInformation("Prompting the user to enter a custom icon for {Host}", host);

        try
        {
            var label = await ParentPage.DisplayPromptAsync(
                "Custom Icon", "Enter what you would like this tab's icon to be.\nOne or two letters or numbers may be entered, or a single emoji character.",
                maxLength: 2, keyboard: Keyboard.Chat);

            if (string.IsNullOrWhiteSpace(label))
                return;

            e.Tab.Label = label;

            _browsingDatabase.Update(e.Tab);

            if (_browsingDatabase.TryGetCapsule(host, out var capsule))
            {
                capsule.Icon = label;
                _browsingDatabase.Update(capsule);
            }
            else
            {
                _browsingDatabase.InsertOrReplace(new Capsule
                {
                    Host = host,
                    Icon = label
                });
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to reset icon for {Host}", host);
            throw;
        }
    }
}