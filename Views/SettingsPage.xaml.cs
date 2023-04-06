using Newtonsoft.Json;
using RosyCrow.Interfaces;
using RosyCrow.Models;

namespace RosyCrow.Views;

public partial class SettingsPage : ContentPage
{
    private readonly ISettingsDatabase _settingsDatabase;

    private IList<ThemeChoice> _choices;
    private ThemeChoice _selectedTheme;

    public SettingsPage(ISettingsDatabase settingsDatabase)
	{
        InitializeComponent();

        _settingsDatabase = settingsDatabase;

        BindingContext = this;
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
        await using var file = await FileSystem.OpenAppPackageFileAsync("themes.json");
        using var reader = new StreamReader(file);
        Choices = JsonConvert.DeserializeObject<ThemeChoice[]>(await reader.ReadToEndAsync());
        SelectedTheme = Choices?.FirstOrDefault(c => c.File == _settingsDatabase.Theme);
    }
}