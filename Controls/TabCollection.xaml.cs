using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Globalization;
using System.Text;
using System.Windows.Input;
using CommunityToolkit.Maui.Alerts;
using CommunityToolkit.Maui.Core;
using CommunityToolkit.Maui.Storage;
using Microsoft.Extensions.Logging;
using Newtonsoft.Json;
using Opal;
using Opal.Response;
using Opal.Tofu;
using RosyCrow.Controls.Tabs;
using RosyCrow.Extensions;
using RosyCrow.Interfaces;
using RosyCrow.Models;
using RosyCrow.Models.Serialization;
using Tab = RosyCrow.Models.Tab;

// ReSharper disable AsyncVoidLambda

namespace RosyCrow.Controls;

public partial class TabCollection : ContentView
{
    private readonly IBrowsingDatabase _browsingDatabase;
    private readonly ILogger<TabCollection> _logger;
    private readonly ISettingsDatabase _settingsDatabase;
    private ICommand _addNewTab;
    private bool _isReordering;

    private Tab _selectedTab;
    private ObservableCollection<Tab> _tabs;
    private bool _tabsEnabled;
    private TabSide _tabSide;

    public TabCollection()
        : this(
            MauiProgram.Services.GetRequiredService<IBrowsingDatabase>(),
            MauiProgram.Services.GetRequiredService<ILogger<TabCollection>>(),
            MauiProgram.Services.GetRequiredService<ISettingsDatabase>())
    {
    }

    public TabCollection(IBrowsingDatabase browsingDatabase, ILogger<TabCollection> logger, ISettingsDatabase settingsDatabase)
    {
        _browsingDatabase = browsingDatabase;
        _logger = logger;
        _settingsDatabase = settingsDatabase;
        _settingsDatabase.PropertyChanged += OnSettingsChanged;

        InitializeComponent();

        BindingContext = this;

        Tabs = _browsingDatabase.Tabs;
        TabSide = _settingsDatabase.TabSide;
        TabsEnabled = _settingsDatabase.TabsEnabled;
        AddNewTab = new Command(async () => await AddDefaultTab(), () => !IsReordering);
    }

    public TabSide TabSide
    {
        get => _tabSide;
        set
        {
            if (value == _tabSide) return;

            _tabSide = value;
            OnPropertyChanged();
        }
    }

    public bool TabsEnabled
    {
        get => _tabsEnabled;
        set
        {
            if (value == _tabsEnabled) return;

            _tabsEnabled = value;
            OnPropertyChanged();
        }
    }

    public Tab SelectedTab
    {
        get => _selectedTab;
        set
        {
            if (Equals(value, _selectedTab))
                return;

            SelectTab(value);
            OnPropertyChanged();
        }
    }

    public ICommand AddNewTab
    {
        get => _addNewTab;
        set
        {
            if (Equals(value, _addNewTab)) return;

            _addNewTab = value;
            OnPropertyChanged();
        }
    }

    public bool IsReordering
    {
        get => _isReordering;
        set
        {
            if (value == _isReordering) return;

            _isReordering = value;
            foreach (var tab in Tabs)
                tab.HandleReordering?.Execute(_isReordering);

            OnReorderingChanged();
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

    private void OnSettingsChanged(object sender, PropertyChangedEventArgs e)
    {
        switch (e.PropertyName)
        {
            case nameof(ISettingsDatabase.TabSide):
                TabSide = _settingsDatabase.TabSide;
                break;
            case nameof(ISettingsDatabase.TabsEnabled):
                TabsEnabled = _settingsDatabase.TabsEnabled;
                break;
        }
    }

    protected override void OnApplyTemplate()
    {
        base.OnApplyTemplate();
        Handler?.UpdateValue(nameof(Content));
    }

    public event EventHandler BookmarkChanged;
    public event EventHandler SelectedTabChanged;
    public event EventHandler ReorderingChanged;

    public void SelectTab(Tab tab)
    {
        if (IsReordering)
            IsReordering = false;

        if (!tab?.InitializedByTabCollection ?? false)
        {
            // the tab was just loaded from the database and hasn't been initialized yet
            SetupTab(tab);
        }

        if (_selectedTab != null)
        {
            _selectedTab.Selected = false;
            _browsingDatabase.Update(_selectedTab);
        }

        _selectedTab = tab;
        OnSelectedTabChanged();

        if (tab != null)
        {
            tab.Selected = true;
            _browsingDatabase.Update(tab);
        }
    }

    private void SetupTab(Tab tab)
    {
        tab.ParentPage = ParentPage;
        tab.OpeningUrlInNewTab += (_, arg) => AddTab(arg.Uri);
        tab.BookmarkChanged += BookmarkChanged;
        tab.InitializedByTabCollection = true;
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

        var tab = new Tab(url, label);
        SelectTab(tab);

        Tabs.Add(tab);

        return _browsingDatabase.UpdateTabOrder();
    }

    public Task ImportTabs(IEnumerable<Tab> tabs)
    {
        foreach (var tab in tabs)
        {
            if (string.IsNullOrWhiteSpace(tab.Label))
            {
                tab.Label = tab.Url.ToGeminiUri().Host[..1];
            }
            else
            {
                var info = new StringInfo(tab.Label);

                if (info.LengthInTextElements > 1)
                {
                    _logger.LogDebug("Truncating an imported tab's label (initially length {Length})", info.LengthInTextElements);
                    tab.Label = info.SubstringByTextElements(0, 1);
                }
            }

            Tabs.Add(tab);
        }

        return _browsingDatabase.UpdateTabOrder();
    }

    private void BrowserTab_OnSelected(object sender, TabEventArgs e)
    {
        SelectedTab = e.Tab;
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
            SelectedTab = next ?? _tabs.First();
        }
    }

    private async void TabsCollectionView_OnReorderCompleted(object sender, EventArgs e)
    {
        await _browsingDatabase.UpdateTabOrder();
    }

    private void TabCollection_OnLoaded(object sender, EventArgs e)
    {
        if (Tabs.FirstOrDefault(t => t.Selected) is { } tab)
            SelectedTab = tab;

        // okay NOW you can start overwriting SelectedTab
    }

    private async void BrowserTab_OnFetchingIcon(object sender, TabEventArgs e)
    {
        var host = e.Tab?.Url?.ToGeminiUri().Host;
        if (string.IsNullOrWhiteSpace(host))
            return;

        try
        {
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
        var host = e.Tab?.Location?.Host;
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

            e.Tab.Label = e.Tab.DefaultLabel;
            _browsingDatabase.Update(e.Tab);
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
            var label = await ParentPage.DisplayPromptOnMainThread(
                "Custom Icon",
                "Enter what you would like this tab's icon to be.\nOne or two letters or numbers may be entered, or a single emoji character.",
                maxLength: 2,
                keyboard: Keyboard.Chat);

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

    private async void BrowserTab_OnReorderingRequested(object sender, EventArgs e)
    {
        await Toast.Make("Go back or select a tab when finished").Show();
        IsReordering = true;
    }

    private async void BrowserTab_OnRemoveAllRequested(object sender, EventArgs e)
    {
        if (await ParentPage.DisplayAlertOnMainThread("Close All", "All tabs will be closed.\nProceed?", "Yes", "No"))
        {
            Tabs.Clear();
            await AddDefaultTab();
        }
    }

    private async void BrowserTab_OnImportRequested(object sender, EventArgs e)
    {
        var jsonFileType = new FilePickerFileType(
            new Dictionary<DevicePlatform, IEnumerable<string>>
            {
                { DevicePlatform.Android, new[] { "application/json" } }
            });

        try
        {
            var file = await FilePicker.Default.PickAsync(new PickOptions
                { FileTypes = jsonFileType, PickerTitle = "Select a JSON File to Import" });
            if (file == null)
                return;

            _logger.LogInformation("Attempting to import tabs from {Path}", file.FullPath);

            await using var fileStream = await file.OpenReadAsync();
            using var reader = new StreamReader(fileStream);
            var imported = JsonConvert.DeserializeObject<SerializedTab[]>(await reader.ReadToEndAsync());

            if (imported.Length == 0)
            {
                await Toast.Make("There are no tabs to import").Show();
            }

            await ImportTabs(imported.Select(t => new Tab
            {
                Url = t.Url,
                Label = t.Icon
            }));

            _logger.LogInformation("Imported {Count} tabs from {Path}", imported.Length, file.FullPath);

            if (imported.Length == 1)
                await Toast.Make("Imported a tab").Show();
            else
                await Toast.Make($"Imported {imported.Length} tabs").Show();
        }
        catch (JsonException ex)
        {
            _logger.LogError(ex, "Failed to import tabs due to malformed JSON");
            await Toast.Make("The file is formatted incorrectly", ToastDuration.Long).Show();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to import tabs");
            await Toast.Make("No tabs were imported because something went wrong", ToastDuration.Long).Show();
        }
    }

    private async void BrowserTab_OnExportRequested(object sender, EventArgs e)
    {
#if ANDROID21_0_OR_GREATER
        try
        {
            _logger.LogInformation("Exporting tabs");

            // storage permission doesn't apply starting in 33
            if (!OperatingSystem.IsAndroidVersionAtLeast(33) &&
                await Permissions.CheckStatusAsync<Permissions.StorageWrite>() != PermissionStatus.Granted)
            {
                _logger.LogInformation("Requesting permission to write to external storage");

                var status = await Permissions.RequestAsync<Permissions.StorageWrite>();

                if (status != PermissionStatus.Granted && Permissions.ShouldShowRationale<Permissions.StorageWrite>())
                {
                    await ParentPage.DisplayAlertOnMainThread("Lacking Permission",
                        "Tabs cannot be exported unless Rosy Crow has permission to write to you device's storage.\n\nTry again after you've granted the app permission to do so.",
                        "OK");
                    return;
                }
            }

            var tabs = Tabs.Select(t => new SerializedTab
            {
                Icon = t.Label,
                Url = t.Url
            }).ToArray();

            FileSaverResult result;

            await using (var buffer = new MemoryStream())
            {
                await using (var writer = new StreamWriter(buffer, leaveOpen: true))
                {
                    await writer.WriteAsync(JsonConvert.SerializeObject(tabs));
                }

                buffer.Seek(0, SeekOrigin.Begin);
                result = await FileSaver.Default.SaveAsync($"tabs_{DateTimeOffset.UtcNow.ToUnixTimeSeconds()}.json",
                    buffer,
                    CancellationToken.None);
            }

            if (result.IsSuccessful)
                _logger.LogInformation("Exported {Count} tabs to {Path}", tabs.Length, result.FilePath);
            else
                await Toast.Make("Could not save the file").Show();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to export tabs");
            await Toast.Make("Nothing was exported because something went wrong", ToastDuration.Long).Show();
        }
#endif
    }

    protected virtual void OnSelectedTabChanged()
    {
        SelectedTabChanged?.Invoke(this, EventArgs.Empty);
    }

    protected virtual void OnReorderingChanged()
    {
        ReorderingChanged?.Invoke(this, EventArgs.Empty);
    }
}