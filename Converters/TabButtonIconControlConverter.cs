using System.Globalization;

namespace RosyCrow.Converters;

public class TabButtonIconControlConverter : BindableObject, IValueConverter
{
    public static BindableProperty DeleteIconTemplateProperty =
        BindableProperty.Create(nameof(DeleteIconTemplate), typeof(ControlTemplate), typeof(TabButtonIconControlConverter));

    public static BindableProperty PageIconTemplateProperty =
        BindableProperty.Create(nameof(PageIconTemplate), typeof(ControlTemplate), typeof(TabButtonIconControlConverter));

    public ControlTemplate PageIconTemplate
    {
        get => (ControlTemplate)GetValue(PageIconTemplateProperty);
        set => SetValue(PageIconTemplateProperty, value);
    }

    public ControlTemplate DeleteIconTemplate
    {
        get => (ControlTemplate)GetValue(DeleteIconTemplateProperty);
        set => SetValue(DeleteIconTemplateProperty, value);
    }

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