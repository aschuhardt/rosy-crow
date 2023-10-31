using System.ComponentModel;
using System.Globalization;
using RosyCrow.Extensions;
using RosyCrow.Interfaces;

namespace RosyCrow.Converters;

public class HomeButtonImageConverter : IValueConverter
{
    private readonly ImageSourceConverter _imageSourceConverter;
    private readonly ISettingsDatabase _settings;

    public HomeButtonImageConverter()
        : this(MauiProgram.Services.GetRequiredService<ISettingsDatabase>())
    {
    }

    public HomeButtonImageConverter(ISettingsDatabase settings)
    {
        _settings = settings;
        _imageSourceConverter = new ImageSourceConverter();
    }

    [Localizable(false)]
    public object Convert(object values, Type targetType, object parameter, CultureInfo culture)
    {
        var isHome = false;
        if (!string.IsNullOrEmpty(_settings.HomeUrl) && values is Uri location)
        {
            isHome = location.AreGeminiUrlsEqual(_settings.HomeUrl);
        }

        return Application.Current!.RequestedTheme switch
        {
            AppTheme.Dark => _imageSourceConverter.ConvertFrom(
                isHome ? "home_fill_light.png" : "home_line_light.png"),
            _ => _imageSourceConverter.ConvertFrom(isHome ? "home_fill_dark.png" : "home_line_dark.png")
        };
    }

    public object ConvertBack(object value, Type targetTypes, object parameter, CultureInfo culture)
    {
        throw new NotImplementedException();
    }
}