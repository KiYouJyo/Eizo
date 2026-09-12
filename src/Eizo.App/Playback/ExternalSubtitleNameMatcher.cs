using System.Text;

namespace Eizo.PlaybackSupport;

internal static class ExternalSubtitleNameMatcher
{
    private static readonly HashSet<string> SupportedExtensions =
        new(StringComparer.OrdinalIgnoreCase)
        {
            ".srt",
            ".vtt",
            ".ass",
            ".ssa",
        };

    internal static bool IsSupported(string fileName) =>
        SupportedExtensions.Contains(Path.GetExtension(fileName));

    internal static bool IsMatch(string subtitleName, string mediaName)
    {
        if (!IsSupported(subtitleName))
            return false;

        var subtitleStem = NormalizeStem(
            Path.GetFileNameWithoutExtension(subtitleName));
        var mediaStem = NormalizeStem(
            Path.GetFileNameWithoutExtension(mediaName));

        if (subtitleStem.Length == 0 || mediaStem.Length == 0)
            return false;

        if (string.Equals(
                subtitleStem,
                mediaStem,
                StringComparison.OrdinalIgnoreCase))
        {
            return true;
        }

        foreach (var separator in new[] { ".", " ", "_", "-" })
        {
            if (subtitleStem.StartsWith(
                    mediaStem + separator,
                    StringComparison.OrdinalIgnoreCase))
            {
                return true;
            }
        }

        return false;
    }

    private static string NormalizeStem(string value)
    {
        if (string.IsNullOrWhiteSpace(value))
            return string.Empty;

        value = value
            .Normalize(NormalizationForm.FormKC)
            .Trim();

        var builder = new StringBuilder(value.Length);
        var previousWasSpace = false;

        foreach (var character in value)
        {
            // WebDAV servers and subtitle tools occasionally preserve format
            // characters that are invisible in Explorer but break ordinal
            // filename-family matching.
            if (character is
                '\u200B' or
                '\u200C' or
                '\u200D' or
                '\u2060' or
                '\uFEFF')
            {
                continue;
            }

            if (char.IsWhiteSpace(character))
            {
                if (!previousWasSpace)
                    builder.Append(' ');

                previousWasSpace = true;
                continue;
            }

            previousWasSpace = false;
            builder.Append(character);
        }

        return builder.ToString().Trim();
    }
}
