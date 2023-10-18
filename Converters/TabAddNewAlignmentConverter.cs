using System.Globalization;
using RosyCrow.Models;

namespace RosyCrow.Converters;

public class TabAddNewAlignmentConverter : BindableObject, IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
    {
        if (value is TabSide side)
            return side == TabSide.Right ? LayoutOptions.End : LayoutOptions.Start;

        throw new ArgumentOutOfRangeException(nameof(value));
    }

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
    {
        throw new NotImplementedException();
    }
}