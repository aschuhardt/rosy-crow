using System.Globalization;
using RosyCrow.Extensions;

namespace RosyCrow.Converters;

internal class TabButtonLabelMarginConverter : BindableObject, IValueConverter
{
    public static readonly BindableProperty EmojiMarginProperty =
        BindableProperty.Create(nameof(EmojiMargin), typeof(Thickness), typeof(TabButtonFontFamilyConverter));

    public static readonly BindableProperty TextMarginProperty =
        BindableProperty.Create(nameof(TextMargin), typeof(Thickness), typeof(TabButtonFontFamilyConverter));

    public Thickness EmojiMargin
    {
        get => (Thickness)GetValue(EmojiMarginProperty);
        set => SetValue(EmojiMarginProperty, value);
    }

    public Thickness TextMargin
    {
        get => (Thickness)GetValue(TextMarginProperty);
        set => SetValue(TextMarginProperty, value);
    }

    public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
    {
        if (value is string label && label.IsEmoji())
            return EmojiMargin;

        return TextMargin;
    }

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
    {
        throw new NotImplementedException();
    }
}