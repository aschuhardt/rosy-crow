using System.Globalization;

namespace RosyCrow.Converters;

public class TabButtonFillConverter : BindableObject, IValueConverter
{
    public static readonly BindableProperty InactiveBrushProperty =
        BindableProperty.Create(nameof(InactiveBrush), typeof(Brush), typeof(TabButtonFillConverter));

    public static readonly BindableProperty ActiveBrushProperty =
        BindableProperty.Create(nameof(ActiveBrush), typeof(Brush), typeof(TabButtonFillConverter));

    public Brush InactiveBrush
    {
        get => (Brush)GetValue(InactiveBrushProperty);
        set => SetValue(InactiveBrushProperty, value);
    }

    public Brush ActiveBrush
    {
        get => (Brush)GetValue(ActiveBrushProperty);
        set => SetValue(ActiveBrushProperty, value);
    }

    public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
    {
        return (bool)value ? ActiveBrush : InactiveBrush;
    }

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
    {
        throw new NotImplementedException();
    }
}