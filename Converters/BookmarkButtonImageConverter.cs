using System.Globalization;
using RosyCrow.Interfaces;

namespace RosyCrow.Converters;

public class BookmarkButtonImageConverter : IValueConverter
{
    private readonly IBrowsingDatabase _browsingDatabase;
    private readonly ImageSourceConverter _imageSourceConverter;

    public BookmarkButtonImageConverter() 
        : this(MauiProgram.Services.GetRequiredService<IBrowsingDatabase>())
    {
    }

    public BookmarkButtonImageConverter(IBrowsingDatabase browsingDatabase)
    {
        _browsingDatabase = browsingDatabase;
        _imageSourceConverter = new ImageSourceConverter();
    }

    public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
    {
        var isBookmark = false;
        if (value is Uri location)
            isBookmark = _browsingDatabase.IsBookmark(location, out _);

        return Application.Current!.RequestedTheme switch
        {
            AppTheme.Dark => _imageSourceConverter.ConvertFrom(isBookmark
                ? "bookmark_fill_light.png"
                : "bookmark_line_light.png"),
            _ => _imageSourceConverter.ConvertFrom(
                isBookmark ? "bookmark_fill_dark.png" : "bookmark_line_dark.png")
        };
    }

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
    {
        throw new NotImplementedException();
    }
}