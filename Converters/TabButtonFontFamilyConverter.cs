using System.Globalization;
using Kotlin.Text;

namespace RosyCrow.Converters;

internal class TabButtonFontFamilyConverter : BindableObject, IValueConverter
{
    private static readonly Regex _emojiPattern = new(@"\p{So}|\p{Cs}\p{Cs}(\p{Cf}\p{Cs}\p{Cs})*");

    public static BindableProperty EmojiFamilyProperty =
        BindableProperty.Create(nameof(EmojiFamily), typeof(string), typeof(TabButtonFontFamilyConverter));

    public static BindableProperty TextFamilyProperty =
        BindableProperty.Create(nameof(TextFamily), typeof(string), typeof(TabButtonFontFamilyConverter));

    public string EmojiFamily
    {
        get => (string)GetValue(EmojiFamilyProperty);
        set => SetValue(EmojiFamilyProperty, value);
    }

    public string TextFamily
    {
        get => (string)GetValue(TextFamilyProperty);
        set => SetValue(TextFamilyProperty, value);
    }

    public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
    {
        if (value is string label && _emojiPattern.Matches(label))
            return EmojiFamily;

        return TextFamily;
    }

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
    {
        throw new NotImplementedException();
    }
}