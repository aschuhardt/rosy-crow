using System.Text.RegularExpressions;

namespace RosyCrow.Extensions;

internal static class StringExtensions
{
    private static readonly Regex _uriPrefixPattern = new ("^[a-zA-Z]+:\\/\\/", RegexOptions.Compiled);
    private const string GeminiSchemePrefix = "gemini://";

    public static Uri ToUri(this string source)
    {
        // The .NET Uri parser is a pain in the ass because it will always default to using the HTTP scheme.
        //
        // Attempting to parse a URL without a scheme will set the port to the default HTTP port, and overriding
        // the scheme will not update the port.  This means that any port information is lost if using the default
        // parser.  The only way to support a scheme-less non-HTTP-by-default URL with a custom port is to write
        // some extra logic to spoon-feed the .NET parser.

        // happy path: starts with gemini schema or no schema
        if (source.StartsWith(GeminiSchemePrefix, StringComparison.OrdinalIgnoreCase) ||
            _uriPrefixPattern.Match(source) is not { Success: true })
            return source.ToGeminiUri();

        // non-Gemini URL; don't assume HTTP/S, could potentially be FTP or anything else
        return new Uri(source);
    }

    public static Uri ToGeminiUri(this string source)
    {
        return !source.StartsWith(GeminiSchemePrefix, StringComparison.OrdinalIgnoreCase)
            ? new Uri($"{GeminiSchemePrefix}{source}")
            : new Uri(source);
    }
}