using System.Globalization;

namespace RosyCrow.Converters;

public class ExpandButtonImageConverter : IValueConverter
{
    private readonly ImageSourceConverter _imageSourceConverter;

    public ExpandButtonImageConverter()
    {
        _imageSourceConverter = new ImageSourceConverter();
    }

    public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
    {
        var isExpanded = (bool)value;

        return Application.Current!.RequestedTheme switch
        {
            AppTheme.Dark => _imageSourceConverter.ConvertFrom(isExpanded
                ? "expand_less_light.png"
                : "expand_more_light.png"),
            _ => _imageSourceConverter.ConvertFrom(
                isExpanded ? "expand_less_dark.png" : "expand_more_dark.png")
        };
    }

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
    {
        throw new NotImplementedException();
    }
}