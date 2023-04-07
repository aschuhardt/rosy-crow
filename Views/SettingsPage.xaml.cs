using Newtonsoft.Json;
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

    public SettingsPage(ISettingsDatabase settingsDatabase, MainPage mainPage)
    {
        InitializeComponent();

        BindingContext = this;

        _settingsDatabase = settingsDatabase;
        _mainPage = mainPage;
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

    private async void SettingsPage_OnLoaded(object sender, EventArgs e)
    {
        if (Choices != null)
            return;

        await using var file = await FileSystem.OpenAppPackageFileAsync("themes.json");
        using var reader = new StreamReader(file);
        Choices = JsonConvert.DeserializeObject<ThemeChoice[]>(await reader.ReadToEndAsync());
        SelectedTheme = Choices?.FirstOrDefault(c => c.File == _settingsDatabase.Theme);
        ThemePreviewBrowser.Location = new Uri($"{Constants.InternalScheme}://preview");
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