using System.Globalization;
using Kotlin.Text;

namespace RosyCrow.Converters;

internal class TabButtonLabelMarginConverter : BindableObject, IValueConverter
{
    private static readonly Regex _emojiPattern = new(@"\p{So}|\p{Cs}\p{Cs}(\p{Cf}\p{Cs}\p{Cs})*");

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
        if (value is string label && _emojiPattern.Matches(label))
            return EmojiMargin;

        return TextMargin;
    }

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
    {
        throw new NotImplementedException();
    }
}