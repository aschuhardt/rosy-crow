namespace Yarrow.Extensions;

internal static class StringExtensions
{
    public static Uri ToGeminiUri(this string source)
    {
        const string schemePrefix = "gemini://";
        return !source.StartsWith(schemePrefix, StringComparison.OrdinalIgnoreCase)
            ? new Uri($"{schemePrefix}{source}")
            : new Uri(source);
    }
}