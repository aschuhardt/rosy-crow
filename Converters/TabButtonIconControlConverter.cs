using System.Globalization;

namespace RosyCrow.Converters;

public class TabButtonIconControlConverter : IValueConverter
{
    public BindableProperty DeleteIconTemplateProperty =
        BindableProperty.Create(nameof(DeleteIconTemplate), typeof(ControlTemplate), typeof(TabButtonIconControlConverter));

    public BindableProperty PageIconTemplateProperty =
        BindableProperty.Create(nameof(PageIconTemplate), typeof(ControlTemplate), typeof(TabButtonIconControlConverter));

    public ControlTemplate PageIconTemplate { get; set; }
    public ControlTemplate DeleteIconTemplate { get; set; }

    public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
    {
        if (value is not bool selected)
            throw new InvalidOperationException();

        return selected ? DeleteIconTemplate : PageIconTemplate;
    }

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
    {
        throw new NotImplementedException();
    }
}