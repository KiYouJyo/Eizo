using System.Net;
using System.Text.RegularExpressions;

namespace Eizo.Models;

internal static partial class BangumiCommunityText
{
    public static string ToPlainText(string? input)
    {
        if (string.IsNullOrWhiteSpace(input))
            return string.Empty;

        var value = WebUtility.HtmlDecode(input)
            .Replace("\r\n", "\n", StringComparison.Ordinal)
            .Replace("\r", "\n", StringComparison.Ordinal);

        value = BreakTagRegex().Replace(value, "\n");
        value = UrlTagRegex().Replace(value, "$2 ($1)");
        value = SimpleUrlTagRegex().Replace(value, "$1");
        value = ImageTagRegex().Replace(value, "$1");
        value = AnyBbCodeTagRegex().Replace(value, string.Empty);
        value = ExcessBlankLinesRegex().Replace(value, "\n\n");

        return value.Trim();
    }

    [GeneratedRegex(
        @"\[br\s*/?\]",
        RegexOptions.IgnoreCase | RegexOptions.CultureInvariant)]
    private static partial Regex BreakTagRegex();

    [GeneratedRegex(
        @"\[url=([^\]]+)\](.*?)\[/url\]",
        RegexOptions.IgnoreCase | RegexOptions.CultureInvariant | RegexOptions.Singleline)]
    private static partial Regex UrlTagRegex();

    [GeneratedRegex(
        @"\[url\](.*?)\[/url\]",
        RegexOptions.IgnoreCase | RegexOptions.CultureInvariant | RegexOptions.Singleline)]
    private static partial Regex SimpleUrlTagRegex();

    [GeneratedRegex(
        @"\[img(?:=[^\]]+)?\](.*?)\[/img\]",
        RegexOptions.IgnoreCase | RegexOptions.CultureInvariant | RegexOptions.Singleline)]
    private static partial Regex ImageTagRegex();

    [GeneratedRegex(
        @"\[/?[a-z][^\]]*\]",
        RegexOptions.IgnoreCase | RegexOptions.CultureInvariant)]
    private static partial Regex AnyBbCodeTagRegex();

    [GeneratedRegex(
        @"\n{3,}",
        RegexOptions.CultureInvariant)]
    private static partial Regex ExcessBlankLinesRegex();
}
