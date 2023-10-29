using System.ComponentModel;
using System.Globalization;

namespace RosyCrow.Converters;

public class VisibilityButtonImageConverter : IValueConverter
{
    private readonly ImageSourceConverter _imageSourceConverter = new();

    [Localizable(false)]
    public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
    {
        var visible = !(bool)value; // value is HidePassword

        return Application.Current!.RequestedTheme switch
        {
            AppTheme.Dark => _imageSourceConverter.ConvertFrom(
                visible ? "visible_light.png" : "invisible_light.png"),
            _ => _imageSourceConverter.ConvertFrom(visible ? "visible_dark.png" : "invisible_dark.png")
        };
    }

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
    {
        throw new NotImplementedException();
    }
}