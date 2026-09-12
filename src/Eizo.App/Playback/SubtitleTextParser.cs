using System.Globalization;
using System.Text.RegularExpressions;

namespace Eizo.PlaybackSupport;

internal sealed record SubtitleCue(
    TimeSpan Start,
    TimeSpan End,
    string Text);

internal sealed class SubtitleDocument
{
    private readonly SubtitleCue[] _cues;
    private readonly TimeSpan _maximumDuration;

    internal SubtitleDocument(IEnumerable<SubtitleCue> cues)
    {
        _cues = cues
            .Where(static cue =>
                cue.End > cue.Start &&
                !string.IsNullOrWhiteSpace(cue.Text))
            .OrderBy(static cue => cue.Start)
            .ThenBy(static cue => cue.End)
            .ToArray();

        _maximumDuration = _cues.Length == 0
            ? TimeSpan.Zero
            : _cues.Max(static cue => cue.End - cue.Start);
    }

    internal IReadOnlyList<SubtitleCue> Cues => _cues;

    internal string? GetText(TimeSpan position)
    {
        if (_cues.Length == 0 || position < TimeSpan.Zero)
            return null;

        var low = 0;
        var high = _cues.Length - 1;
        var lastStarted = -1;

        while (low <= high)
        {
            var middle = low + ((high - low) / 2);
            if (_cues[middle].Start <= position)
            {
                lastStarted = middle;
                low = middle + 1;
            }
            else
            {
                high = middle - 1;
            }
        }

        if (lastStarted < 0)
            return null;

        var matches = new List<SubtitleCue>();
        for (var index = lastStarted; index >= 0; index--)
        {
            var cue = _cues[index];
            if (_maximumDuration > TimeSpan.Zero &&
                position - cue.Start > _maximumDuration)
            {
                break;
            }

            if (cue.Start <= position && cue.End > position)
                matches.Add(cue);
        }

        if (matches.Count == 0)
            return null;

        matches.Reverse();
        return string.Join(
            Environment.NewLine,
            matches.Select(static cue => cue.Text));
    }
}

internal static partial class SubtitleTextParser
{
    internal static SubtitleDocument Parse(
        string extension,
        string text)
    {
        ArgumentNullException.ThrowIfNull(text);

        var normalizedExtension = extension
            .Trim()
            .ToLowerInvariant();

        if (!normalizedExtension.StartsWith('.'))
            normalizedExtension = "." + normalizedExtension;

        return normalizedExtension switch
        {
            ".srt" => ParseTimedText(text, isWebVtt: false),
            ".vtt" => ParseTimedText(text, isWebVtt: true),
            ".ass" or ".ssa" => ParseAss(text),
            _ => new SubtitleDocument([]),
        };
    }

    private static SubtitleDocument ParseTimedText(
        string text,
        bool isWebVtt)
    {
        var normalized = NormalizeLines(text);
        if (isWebVtt && normalized.StartsWith(
                "WEBVTT",
                StringComparison.OrdinalIgnoreCase))
        {
            var firstNewLine = normalized.IndexOf('\n');
            normalized = firstNewLine >= 0
                ? normalized[(firstNewLine + 1)..]
                : string.Empty;
        }

        var blocks = BlankLineRegex()
            .Split(normalized)
            .Where(static block => !string.IsNullOrWhiteSpace(block));

        var cues = new List<SubtitleCue>();

        foreach (var block in blocks)
        {
            var lines = block
                .Split('\n')
                .Select(static line => line.TrimEnd())
                .ToArray();

            var timingIndex = Array.FindIndex(
                lines,
                static line => line.Contains("-->", StringComparison.Ordinal));

            if (timingIndex < 0)
                continue;

            var timing = lines[timingIndex].Split(
                "-->",
                2,
                StringSplitOptions.TrimEntries);

            if (timing.Length != 2 ||
                !TryParseTimestamp(timing[0], out var start))
            {
                continue;
            }

            var endToken = timing[1]
                .Split(
                    ' ',
                    StringSplitOptions.RemoveEmptyEntries |
                    StringSplitOptions.TrimEntries)
                .FirstOrDefault();

            if (endToken is null ||
                !TryParseTimestamp(endToken, out var end) ||
                end <= start)
            {
                continue;
            }

            var body = string.Join(
                Environment.NewLine,
                lines
                    .Skip(timingIndex + 1)
                    .Where(static line => !string.IsNullOrWhiteSpace(line)));

            body = CleanupTimedText(body);
            if (!string.IsNullOrWhiteSpace(body))
                cues.Add(new SubtitleCue(start, end, body));
        }

        return new SubtitleDocument(cues);
    }

    private static SubtitleDocument ParseAss(string text)
    {
        var lines = NormalizeLines(text).Split('\n');
        var inEvents = false;
        string[] format =
        [
            "Layer",
            "Start",
            "End",
            "Style",
            "Name",
            "MarginL",
            "MarginR",
            "MarginV",
            "Effect",
            "Text"
        ];

        var cues = new List<SubtitleCue>();

        foreach (var rawLine in lines)
        {
            var line = rawLine.Trim();

            if (line.StartsWith('[') && line.EndsWith(']'))
            {
                inEvents = string.Equals(
                    line,
                    "[Events]",
                    StringComparison.OrdinalIgnoreCase);
                continue;
            }

            if (!inEvents)
                continue;

            if (line.StartsWith(
                    "Format:",
                    StringComparison.OrdinalIgnoreCase))
            {
                format = line["Format:".Length..]
                    .Split(',')
                    .Select(static value => value.Trim())
                    .ToArray();
                continue;
            }

            if (!line.StartsWith(
                    "Dialogue:",
                    StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }

            var payload = line["Dialogue:".Length..].TrimStart();
            var values = SplitLimited(payload, Math.Max(1, format.Length));
            var startIndex = Array.FindIndex(
                format,
                static value => string.Equals(
                    value,
                    "Start",
                    StringComparison.OrdinalIgnoreCase));
            var endIndex = Array.FindIndex(
                format,
                static value => string.Equals(
                    value,
                    "End",
                    StringComparison.OrdinalIgnoreCase));
            var textIndex = Array.FindIndex(
                format,
                static value => string.Equals(
                    value,
                    "Text",
                    StringComparison.OrdinalIgnoreCase));

            if (startIndex < 0 ||
                endIndex < 0 ||
                textIndex < 0 ||
                startIndex >= values.Length ||
                endIndex >= values.Length ||
                textIndex >= values.Length ||
                !TryParseTimestamp(values[startIndex], out var start) ||
                !TryParseTimestamp(values[endIndex], out var end) ||
                end <= start)
            {
                continue;
            }

            var body = CleanupAssText(values[textIndex]);
            if (!string.IsNullOrWhiteSpace(body))
                cues.Add(new SubtitleCue(start, end, body));
        }

        return new SubtitleDocument(cues);
    }

    private static string[] SplitLimited(
        string value,
        int fieldCount)
    {
        if (fieldCount <= 1)
            return [value];

        var result = new string[fieldCount];
        var start = 0;

        for (var index = 0; index < fieldCount - 1; index++)
        {
            var comma = value.IndexOf(',', start);
            if (comma < 0)
            {
                result[index] = value[start..];
                for (var empty = index + 1; empty < fieldCount; empty++)
                    result[empty] = string.Empty;
                return result;
            }

            result[index] = value[start..comma];
            start = comma + 1;
        }

        result[^1] = start <= value.Length
            ? value[start..]
            : string.Empty;
        return result;
    }

    private static bool TryParseTimestamp(
        string value,
        out TimeSpan timestamp)
    {
        timestamp = TimeSpan.Zero;

        var token = value
            .Trim()
            .Replace(',', '.');

        var parts = token.Split(':');
        if (parts.Length is < 2 or > 3)
            return false;

        if (!double.TryParse(
                parts[^1],
                NumberStyles.AllowDecimalPoint,
                CultureInfo.InvariantCulture,
                out var seconds) ||
            seconds < 0d)
        {
            return false;
        }

        if (!int.TryParse(
                parts[^2],
                NumberStyles.None,
                CultureInfo.InvariantCulture,
                out var minutes) ||
            minutes < 0)
        {
            return false;
        }

        var hours = 0;
        if (parts.Length == 3 &&
            (!int.TryParse(
                 parts[0],
                 NumberStyles.None,
                 CultureInfo.InvariantCulture,
                 out hours) ||
             hours < 0))
        {
            return false;
        }

        timestamp =
            TimeSpan.FromHours(hours) +
            TimeSpan.FromMinutes(minutes) +
            TimeSpan.FromSeconds(seconds);
        return true;
    }

    private static string CleanupTimedText(string text) =>
        HtmlTagRegex()
            .Replace(text, string.Empty)
            .Replace("&nbsp;", " ", StringComparison.OrdinalIgnoreCase)
            .Trim();

    private static string CleanupAssText(string text) =>
        AssOverrideRegex()
            .Replace(text, string.Empty)
            .Replace("\\N", Environment.NewLine, StringComparison.Ordinal)
            .Replace("\\n", Environment.NewLine, StringComparison.Ordinal)
            .Replace("\\h", " ", StringComparison.Ordinal)
            .Trim();

    private static string NormalizeLines(string value) =>
        value
            .Replace("\r\n", "\n", StringComparison.Ordinal)
            .Replace('\r', '\n');

    [GeneratedRegex(@"\n\s*\n+", RegexOptions.CultureInvariant)]
    private static partial Regex BlankLineRegex();

    [GeneratedRegex(@"<[^>]+>", RegexOptions.CultureInvariant)]
    private static partial Regex HtmlTagRegex();

    [GeneratedRegex(@"\{[^}]*\}", RegexOptions.CultureInvariant)]
    private static partial Regex AssOverrideRegex();
}
