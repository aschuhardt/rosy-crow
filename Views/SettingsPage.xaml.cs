using System.Formats.Tar;
using System.IO.Compression;
using System.Windows.Input;
using CommunityToolkit.Maui.Alerts;
using Newtonsoft.Json;
using RosyCrow.Extensions;
using RosyCrow.Interfaces;
using RosyCrow.Models;

// ReSharper disable AsyncVoidLambda

namespace RosyCrow.Views;

public partial class SettingsPage : ContentPage
{
    private readonly MainPage _mainPage;
    private readonly ISettingsDatabase _settingsDatabase;

    private IList<ThemeChoice> _choices;
    private ThemeChoice _selectedTheme;
    private ICommand _openAbout;
    private ICommand _exportLogs;

    public SettingsPage(ISettingsDatabase settingsDatabase, MainPage mainPage)
    {
        _settingsDatabase = settingsDatabase;
        _mainPage = mainPage;

        InitializeComponent();

        BindingContext = this;

        OpenAbout = new Command(async () => await Navigation.PushPageAsync<AboutPage>());
        ExportLogs = new Command(async () => await ExportErrorLogArchive());
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

    private async Task ExportErrorLogArchive()
    {
        var logsDir = Path.Combine(FileSystem.AppDataDirectory, "logs");
        if (!Directory.Exists(logsDir) || !Directory.GetFiles(logsDir).Any())
        {
            await Toast.Make("There are no error logs to export").Show();
            return;
        }

        var path = Path.Combine(Path.GetTempPath(), $"rosycrow_logs_{DateTimeOffset.UtcNow.ToUnixTimeMilliseconds()}.tar.gz");
        await using var file = File.OpenWrite(path);
        await using var gzip = new GZipStream(file, CompressionLevel.Optimal);
        await TarFile.CreateFromDirectoryAsync(logsDir, gzip, false);
        // await Launcher.Default.OpenAsync(new OpenFileRequest("Rosy Crow Logs", new ReadOnlyFile(path, "application/gzip")));

        await Share.Default.RequestAsync(new ShareFileRequest("Share error logs", new ShareFile(path, "application/gzip")));
    }

    private async void SettingsPage_OnLoaded(object sender, EventArgs e)
    {
        if (Choices != null)
            return;

        await using var file = await FileSystem.OpenAppPackageFileAsync("themes.json");
        using var reader = new StreamReader(file);
        Choices = JsonConvert.DeserializeObject<ThemeChoice[]>(await reader.ReadToEndAsync());
        SelectedTheme = Choices?.FirstOrDefault(c => c.File == _settingsDatabase.Theme);
        ThemePreviewBrowser.Location = new Uri($"{Constants.InternalScheme}://preview");
        HistoryPageSize = _settingsDatabase.HistoryPageSize;
    }

    private async void Picker_OnSelectedIndexChanged(object sender, EventArgs e)
    {
        await ThemePreviewBrowser.LoadPage();
    }

    private void SettingsPage_OnDisappearing(object sender, EventArgs e)
    {
        _mainPage.LoadPageOnAppearing = true;
    }
}