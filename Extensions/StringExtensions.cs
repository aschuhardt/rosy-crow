﻿using System.Text.RegularExpressions;
using RosyCrow.Models;

namespace RosyCrow.Extensions;

internal static class StringExtensions
{
    private const string GeminiSchemePrefix = "gemini://";
    private static readonly Regex _uriPrefixPattern = new("^[a-zA-Z]+:\\/\\/", RegexOptions.Compiled);
    private static readonly Regex _emojiPattern = new(@"\p{So}|\p{Cs}\p{Cs}(\p{Cf}\p{Cs}\p{Cs})*");

    public static bool IsEmoji(this string source)
    {
        return _emojiPattern.IsMatch(source);
    }

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
        // may be rosy-crow://...
        if (source.StartsWith(Constants.InternalScheme, StringComparison.OrdinalIgnoreCase))
            return new Uri(source);

        return !source.StartsWith(GeminiSchemePrefix, StringComparison.OrdinalIgnoreCase)
            ? new Uri($"{GeminiSchemePrefix}{source}")
            : new Uri(source);
    }

    public static bool AreGeminiUrlsEqual(this string first, Uri second)
    {
        return first.ToGeminiUri().AreGeminiUrlsEqual(second);

    }

    public static bool AreGeminiUrlsEqual(this string first, string second)
    {
        return first.ToGeminiUri().AreGeminiUrlsEqual(second.ToGeminiUri());
    }

    public static bool AreGeminiUrlsEqual(this Uri first, string second)
    {
        return first.AreGeminiUrlsEqual(second.ToGeminiUri());
    }

    public static bool AreGeminiUrlsEqual(this Uri first, Uri second)
    {
        return Uri.Compare(
            first, second,
            UriComponents.HostAndPort | UriComponents.PathAndQuery,
            UriFormat.Unescaped,
            StringComparison.OrdinalIgnoreCase) == 0;
    }

    public static string ToFriendlyFingerprint(this string fingerprint)
    {
        var buffer = Convert.FromHexString(fingerprint);
        return BitConverter.ToString(buffer).Replace('-', ' ');
    }
}