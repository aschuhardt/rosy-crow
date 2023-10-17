using System.Globalization;

namespace RosyCrow.Converters;

public class TabButtonIconControlConverter : BindableObject, IValueConverter
{
    public static BindableProperty SelectedIconTemplateProperty =
        BindableProperty.Create(nameof(SelectedIconTemplate), typeof(ControlTemplate), typeof(TabButtonIconControlConverter));

    public static BindableProperty UnselectedIconTemplateProperty =
        BindableProperty.Create(nameof(UnselectedIconTemplate), typeof(ControlTemplate), typeof(TabButtonIconControlConverter));

    public ControlTemplate UnselectedIconTemplate
    {
        get => (ControlTemplate)GetValue(UnselectedIconTemplateProperty);
        set => SetValue(UnselectedIconTemplateProperty, value);
    }

    public ControlTemplate SelectedIconTemplate
    {
        get => (ControlTemplate)GetValue(SelectedIconTemplateProperty);
        set => SetValue(SelectedIconTemplateProperty, value);
    }

    public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
    {
        if (value is not bool selected)
            throw new InvalidOperationException();

        return selected ? SelectedIconTemplate : UnselectedIconTemplate;
    }

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
    {
        throw new NotImplementedException();
    }
}