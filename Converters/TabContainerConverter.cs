using System.Globalization;
using System.Runtime.CompilerServices;
using RosyCrow.Models;

namespace RosyCrow.Converters;

public class TabContainerConverter : BindableObject, IValueConverter
{
    public static readonly BindableProperty LeftSideTemplateProperty =
        BindableProperty.Create(nameof(LeftSideTemplate), typeof(ControlTemplate), typeof(CallConvThiscall));

    public static readonly BindableProperty RightSideTemplateProperty =
        BindableProperty.Create(nameof(RightSideTemplate), typeof(ControlTemplate), typeof(CallConvThiscall));

    public ControlTemplate LeftSideTemplate
    {
        get => (ControlTemplate)GetValue(LeftSideTemplateProperty);
        set => SetValue(LeftSideTemplateProperty, value);
    }

    public ControlTemplate RightSideTemplate
    {
        get => (ControlTemplate)GetValue(RightSideTemplateProperty);
        set => SetValue(RightSideTemplateProperty, value);
    }

    public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
    {
        if (value is TabSide side)
            return side == TabSide.Right ? RightSideTemplate : LeftSideTemplate;

        throw new ArgumentOutOfRangeException(nameof(value));
    }

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
    {
        throw new NotImplementedException();
    }
}