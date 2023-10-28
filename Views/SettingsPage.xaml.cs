using System.ComponentModel;
using System.Formats.Tar;
using System.IO.Compression;
using System.Windows.Input;
using CommunityToolkit.Maui.Alerts;
using CommunityToolkit.Maui.Storage;
using Microsoft.Extensions.Logging;
using Newtonsoft.Json;
using RosyCrow.Extensions;
using RosyCrow.Interfaces;
using RosyCrow.Models;
using RosyCrow.Services.Document;

// ReSharper disable AsyncVoidLambda

namespace RosyCrow.Views;

public partial class SettingsPage : ContentPage
{
    private readonly IDocumentService _documentService;
    private readonly ILogger<SettingsPage> _logger;
    private readonly MainPage _mainPage;
    private readonly ISettingsDatabase _settingsDatabase;

    private IList<ThemeChoice> _choices;
    private ICommand _copyVersion;
    private ICommand _exportLogs;
    private ICommand _openAbout;
    private ICommand _openWhatsNew;
    private ThemeChoice _selectedTheme;
    private string _versionInfo;

    public SettingsPage(ISettingsDatabase settingsDatabase, MainPage mainPage, IDocumentService documentService, ILogger<SettingsPage> logger)
    {
        _settingsDatabase = settingsDatabase;
        _mainPage = mainPage;
        _documentService = documentService;
        _logger = logger;

        InitializeComponent();

        BindingContext = this;

        _settingsDatabase.PropertyChanged += SettingChanged;
        OpenAbout = new Command(async () => await Navigation.PushPageAsync<AboutPage>());
        OpenWhatsNew = new Command(async () => await Navigation.PushPageAsync<WhatsNewPage>());
        ExportLogs = new Command(async () => await ExportErrorLogArchive());
        CopyVersion = new Command(async () =>
        {
            await Clipboard.SetTextAsync(VersionInfo);
            await Toast.Make("Copied").Show();
        });
    }

    private async void SettingChanged(object sender, PropertyChangedEventArgs e)
    {
        switch (e.PropertyName)
        {
            case nameof(ISettingsDatabase.UseCustomCss):
            case nameof(ISettingsDatabase.UseCustomFontSize):
            case nameof(ISettingsDatabase.Theme):
            case nameof(ISettingsDatabase.CustomCss):
            case nameof(ISettingsDatabase.CustomFontSizeText):
            case nameof(ISettingsDatabase.CustomFontSizeH1):
            case nameof(ISettingsDatabase.CustomFontSizeH2):
            case nameof(ISettingsDatabase.CustomFontSizeH3):
                await RefreshPreview();
                break;
        }
    }

    public bool TabsEnabled
    {
        get => _settingsDatabase.TabsEnabled;
        set
        {
            if (value == _settingsDatabase.TabsEnabled)
                return;

            _settingsDatabase.TabsEnabled = value;
            OnPropertyChanged();
        }
    }

    public TabSide TabSide
    {
        get => _settingsDatabase.TabSide;
        set
        {
            if (value == _settingsDatabase.TabSide)
                return;

            _settingsDatabase.TabSide = value;
            OnPropertyChanged();
        }
    }

    public IList<ThemeChoice> Choices
    {
        get => _choices;
        set
        {
            if (Equals(value, _choices)) return;

            _choices = value;
            OnPropertyChanged();
        }
    }

    public ThemeChoice SelectedTheme
    {
        get => _selectedTheme;
        set
        {
            if (Equals(value, _selectedTheme))
                return;

            _selectedTheme = value;
            _settingsDatabase.Theme = value.File;
            OnPropertyChanged();
        }
    }

    public ICommand OpenAbout
    {
        get => _openAbout;
        set
        {
            if (Equals(value, _openAbout)) return;

            _openAbout = value;
            OnPropertyChanged();
        }
    }

    public ICommand OpenWhatsNew
    {
        get => _openWhatsNew;
        set
        {
            if (Equals(value, _openWhatsNew))
                return;

            _openWhatsNew = value;
            OnPropertyChanged();
        }
    }

    public string VersionInfo
    {
        get => _versionInfo;
        set
        {
            if (value == _versionInfo) return;

            _versionInfo = value;
            OnPropertyChanged();
        }
    }

    public ICommand CopyVersion
    {
        get => _copyVersion;
        set
        {
            if (Equals(value, _copyVersion)) return;

            _copyVersion = value;
            OnPropertyChanged();
        }
    }

    public ICommand ExportLogs
    {
        get => _exportLogs;
        set
        {
            if (Equals(value, _exportLogs)) return;

            _exportLogs = value;
            OnPropertyChanged();
        }
    }

    public int HistoryPageSize
    {
        get => _settingsDatabase?.HistoryPageSize ?? default;
        set
        {
            if (value == _settingsDatabase.HistoryPageSize)
                return;

            _settingsDatabase.HistoryPageSize = value;
            OnPropertyChanged();
        }
    }

    public bool AllowIPv6
    {
        get => _settingsDatabase.AllowIpv6;
        set
        {
            if (value == _settingsDatabase.AllowIpv6)
                return;

            _settingsDatabase.AllowIpv6 = value;
            OnPropertyChanged();
        }
    }

    public bool InlineImages
    {
        get => _settingsDatabase?.InlineImages ?? default;
        set
        {
            if (value == _settingsDatabase.InlineImages)
                return;

            _settingsDatabase.InlineImages = value;
            OnPropertyChanged();
        }
    }

    public bool HidePullTab
    {
        get => _settingsDatabase?.HidePullTab ?? default;
        set
        {
            if (value == _settingsDatabase.HidePullTab)
                return;

            _settingsDatabase.HidePullTab = value;
            OnPropertyChanged();
        }
    }

    public bool StrictTofuMode
    {
        get => _settingsDatabase?.StrictTofuMode ?? default;
        set
        {
            if (value == _settingsDatabase.StrictTofuMode)
                return;

            _settingsDatabase.StrictTofuMode = value;
            OnPropertyChanged();
        }
    }

    public bool EnableSwipe
    {
        get => _settingsDatabase?.SwipeEnabled ?? default;
        set
        {
            if (value == _settingsDatabase.SwipeEnabled)
                return;

            _settingsDatabase.SwipeEnabled = value;
            OnPropertyChanged();
        }
    }

    public bool UseCustomFontSize
    {
        get => _settingsDatabase?.UseCustomFontSize ?? false;
        set
        {
            if (value == _settingsDatabase.UseCustomFontSize)
                return;

            _settingsDatabase.UseCustomFontSize = value;
            OnPropertyChanged();
        }
    }

    public int CustomFontSizeH1
    {
        get => _settingsDatabase?.CustomFontSizeH1 ?? 14;
        set
        {
            if (value == _settingsDatabase.CustomFontSizeH1)
                return;

            _settingsDatabase.CustomFontSizeH1 = value;
            OnPropertyChanged();
        }
    }

    public int CustomFontSizeH2
    {
        get => _settingsDatabase?.CustomFontSizeH2 ?? 14;
        set
        {
            if (value == _settingsDatabase.CustomFontSizeH2)
                return;

            _settingsDatabase.CustomFontSizeH2 = value;
            OnPropertyChanged();
        }
    }

    public int CustomFontSizeH3
    {
        get => _settingsDatabase?.CustomFontSizeH3 ?? 14;
        set
        {
            if (value == _settingsDatabase.CustomFontSizeH3)
                return;

            _settingsDatabase.CustomFontSizeH3 = value;
            OnPropertyChanged();
        }
    }

    public int CustomFontSizeText
    {
        get => _settingsDatabase?.CustomFontSizeText ?? 14;
        set
        {
            if (value == _settingsDatabase.CustomFontSizeText)
                return;

            _settingsDatabase.CustomFontSizeText = value;
            OnPropertyChanged();
        }
    }

    public bool UseCustomCss
    {
        get => _settingsDatabase?.UseCustomCss ?? false;
        set
        {
            if (value == _settingsDatabase.UseCustomCss)
                return;

            _settingsDatabase.UseCustomCss = value;
            OnPropertyChanged();
        }
    }

    public string CustomCss
    {
        get => _settingsDatabase?.CustomCss;
        set
        {
            if (value == _settingsDatabase.CustomCss)
                return;

            _settingsDatabase.CustomCss = value;
            OnPropertyChanged();
        }
    }

    private async Task ExportErrorLogArchive()
    {
        // storage permission doesn't apply starting in 33
        if (!OperatingSystem.IsAndroidVersionAtLeast(33) &&
            await Permissions.CheckStatusAsync<Permissions.StorageWrite>() != PermissionStatus.Granted)
        {
            _logger.LogInformation("Requesting permission to write to external storage");

            var status = await Permissions.RequestAsync<Permissions.StorageWrite>();

            if (status != PermissionStatus.Granted && Permissions.ShouldShowRationale<Permissions.StorageWrite>())
            {
                await DisplayAlert("Lacking Permission",
                    "Logs cannot be exported unless Rosy Crow has permission to write to you device's storage.\n\nTry again after you've granted the app permission to do so.",
                    "OK");
                return;
            }
        }

        var logsDir = Path.Combine(FileSystem.AppDataDirectory, "logs");

        if (!Directory.Exists(logsDir) || !Directory.GetFiles(logsDir).Any())
        {
            await Toast.Make("There are no error logs to export").Show();
            return;
        }

        FileSaverResult result;

        await using (var buffer = new MemoryStream())
        {
            await using var gzip = new GZipStream(buffer, CompressionLevel.Optimal);
            await TarFile.CreateFromDirectoryAsync(logsDir, gzip, false);

            buffer.Seek(0, SeekOrigin.Begin);
            result = await FileSaver.Default.SaveAsync($"rosycrow_logs_{DateTimeOffset.UtcNow.ToUnixTimeMilliseconds()}.tar.gz",
                buffer,
                CancellationToken.None);
        }

        if (result.IsSuccessful)
        {
            _logger.LogInformation("Exported log archive to {Path}", result.FilePath);
        }
        else
        {
            await Toast.Make("Could not save archive").Show();
        }
    }

    private async void SettingsPage_OnLoaded(object sender, EventArgs e)
    {
        if (Choices != null)
            return;

        await using (var file = await FileSystem.OpenAppPackageFileAsync("themes.json"))
        using (var reader = new StreamReader(file))
        {
            Choices = JsonConvert.DeserializeObject<ThemeChoice[]>(await reader.ReadToEndAsync());
        }


        SelectedTheme = Choices?.FirstOrDefault(c => c.File == _settingsDatabase.Theme);
        HistoryPageSize = _settingsDatabase.HistoryPageSize;
        VersionInfo = $"Version {VersionTracking.Default.CurrentVersion}, build {VersionTracking.Default.CurrentBuild}";
        TabSide = _settingsDatabase.TabSide;
        TabsEnabled = _settingsDatabase.TabsEnabled;

        await RefreshPreview();
    }

    private async Task RefreshPreview()
    {
        _mainPage.LoadPageOnAppearing = true;
        var html = await _documentService.RenderInternalDocument("preview");
        ThemePreviewBrowser.Source = new HtmlWebViewSource { Html = html };
    }

    private void ThemePreviewBrowser_OnNavigating(object sender, WebNavigatingEventArgs e)
    {
        // do not
    }
}