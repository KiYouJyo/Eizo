using System.Net;
using System.Text.RegularExpressions;

namespace Eizo.Models;

internal sealed record BangumiCommunityContentBlock(
    string Text,
    string? ImageUrl)
{
    public bool IsImage =>
        !string.IsNullOrWhiteSpace(ImageUrl);
}

internal static partial class BangumiCommunityText
{
    private const string BangumiImageRoot =
        "https://lain.bgm.tv/pic/photo/l/";

    public static string ToPlainText(string? input)
    {
        if (string.IsNullOrWhiteSpace(input))
            return string.Empty;

        var value = Normalize(input);

        value = BreakTagRegex().Replace(value, "\n");
        value = UrlTagRegex().Replace(value, "$2 ($1)");
        value = SimpleUrlTagRegex().Replace(value, "$1");
        value = ImageTagRegex().Replace(value, "$1");
        value = AnyBbCodeTagRegex().Replace(value, string.Empty);
        value = ExcessBlankLinesRegex().Replace(value, "\n\n");

        return value.Trim();
    }

    public static IReadOnlyList<BangumiCommunityContentBlock>
        ParseContentBlocks(string? input)
    {
        if (string.IsNullOrWhiteSpace(input))
            return [];

        var normalized = Normalize(input);
        var blocks = new List<BangumiCommunityContentBlock>();
        var cursor = 0;

        foreach (Match match in ImageTagRegex().Matches(normalized))
        {
            AddTextBlocks(
                normalized[cursor..match.Index],
                blocks);

            var target = match.Groups[1].Value.Trim();
            var imageUrl = ResolveImageUrl(target);
            if (!string.IsNullOrWhiteSpace(imageUrl))
            {
                blocks.Add(
                    new BangumiCommunityContentBlock(
                        string.Empty,
                        imageUrl));
            }

            cursor = match.Index + match.Length;
        }

        AddTextBlocks(
            normalized[cursor..],
            blocks);

        return blocks;
    }

    private static void AddTextBlocks(
        string value,
        ICollection<BangumiCommunityContentBlock> blocks)
    {
        if (string.IsNullOrWhiteSpace(value))
            return;

        var lines = value
            .Replace("\r\n", "\n", StringComparison.Ordinal)
            .Replace("\r", "\n", StringComparison.Ordinal)
            .Split('\n');

        var text = new List<string>();

        void FlushText()
        {
            if (text.Count == 0)
                return;

            var plain = ToPlainText(
                string.Join("\n", text));
            if (!string.IsNullOrWhiteSpace(plain))
            {
                blocks.Add(
                    new BangumiCommunityContentBlock(
                        plain,
                        null));
            }

            text.Clear();
        }

        foreach (var line in lines)
        {
            var trimmed = line.Trim();
            var barePhoto =
                BarePhotoPathRegex().Match(trimmed);
            if (barePhoto.Success)
            {
                FlushText();
                var imageUrl = ResolveImageUrl(
                    barePhoto.Groups[1].Value);
                if (!string.IsNullOrWhiteSpace(imageUrl))
                {
                    blocks.Add(
                        new BangumiCommunityContentBlock(
                            string.Empty,
                            imageUrl));
                }

                continue;
            }

            text.Add(line);
        }

        FlushText();
    }

    private static string? ResolveImageUrl(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
            return null;

        var target = WebUtility.HtmlDecode(value).Trim();

        if (Uri.TryCreate(
                target,
                UriKind.Absolute,
                out var absolute) &&
            (string.Equals(
                 absolute.Scheme,
                 Uri.UriSchemeHttp,
                 StringComparison.OrdinalIgnoreCase) ||
             string.Equals(
                 absolute.Scheme,
                 Uri.UriSchemeHttps,
                 StringComparison.OrdinalIgnoreCase)))
        {
            return absolute.ToString();
        }

        target = target.TrimStart('/');

        if (target.StartsWith(
                "pic/",
                StringComparison.OrdinalIgnoreCase))
        {
            return "https://lain.bgm.tv/" + target;
        }

        return BangumiImageRoot + target;
    }

    private static string Normalize(string input) =>
        WebUtility.HtmlDecode(input)
            .Replace("\r\n", "\n", StringComparison.Ordinal)
            .Replace("\r", "\n", StringComparison.Ordinal);

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
        @"^((?:[a-z0-9]{2}/){2}[^\s]+\.(?:jpe?g|png|gif|webp))$",
        RegexOptions.IgnoreCase | RegexOptions.CultureInvariant)]
    private static partial Regex BarePhotoPathRegex();

    [GeneratedRegex(
        @"\[/?[a-z][^\]]*\]",
        RegexOptions.IgnoreCase | RegexOptions.CultureInvariant)]
    private static partial Regex AnyBbCodeTagRegex();

    [GeneratedRegex(
        @"\n{3,}",
        RegexOptions.CultureInvariant)]
    private static partial Regex ExcessBlankLinesRegex();
}
