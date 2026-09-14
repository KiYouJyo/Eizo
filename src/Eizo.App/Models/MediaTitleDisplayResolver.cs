using System.Globalization;
using Eizo.MetadataIntegration;

namespace Eizo.Models;

internal static class MediaTitleDisplayResolver
{
    public static string ResolvePrimary(
        MediaMetadataSnapshot? metadata,
        params string?[] fallbacks)
    {
        if (metadata is { IsResolved: true })
        {
            var language =
                ResolveCurrentLanguage();
            var localized =
                FindLocalizedTitle(
                    metadata.LocalizedTitles,
                    language);

            var preferred = language switch
            {
                "zh-CN" => FirstNonEmpty(
                    localized,
                    metadata.OriginalTitle),
                "en-US" => FirstNonEmpty(
                    localized,
                    metadata.OriginalTitle),
                "ja-JP" => FirstNonEmpty(
                    metadata.OriginalTitle,
                    localized),
                _ => FirstNonEmpty(
                    metadata.OriginalTitle,
                    localized),
            };

            var resolved = FirstNonEmpty(
                preferred,
                metadata.OriginalTitle,
                metadata.CanonicalTitle);

            if (!string.IsNullOrWhiteSpace(resolved))
            {
                return resolved;
            }
        }

        return FirstNonEmpty(fallbacks);
    }

    public static string ResolveSecondary(
        MediaMetadataSnapshot? metadata,
        string? primary,
        params string?[] fallbacks)
    {
        if (metadata is { IsResolved: true })
        {
            var candidates = new[]
            {
                metadata.OriginalTitle,
                metadata.CanonicalTitle,
                FindLocalizedTitle(
                    metadata.LocalizedTitles,
                    ResolveCurrentLanguage()),
            };

            foreach (var candidate in candidates)
            {
                if (IsDistinctTitle(candidate, primary))
                {
                    return candidate!;
                }
            }
        }

        foreach (var fallback in fallbacks)
        {
            if (IsDistinctTitle(fallback, primary))
            {
                return fallback!;
            }
        }

        return string.Empty;
    }

    private static string ResolveCurrentLanguage()
    {
        var language = CultureInfo.CurrentUICulture.Name;
        if (language.StartsWith("ja", StringComparison.OrdinalIgnoreCase))
            return "ja-JP";
        if (language.StartsWith("en", StringComparison.OrdinalIgnoreCase))
            return "en-US";
        if (language.StartsWith("zh", StringComparison.OrdinalIgnoreCase))
            return "zh-CN";
        return "ja-JP";
    }

    private static string? FindLocalizedTitle(
        IReadOnlyDictionary<string, string>? titles,
        string language)
    {
        if (titles is null || titles.Count == 0)
        {
            return null;
        }

        var keys = language switch
        {
            "zh-CN" => new[]
            {
                "zh-CN",
                "zh-Hans",
                "zh",
                "zh-cn",
                "zh-hans",
            },
            "en-US" => new[]
            {
                "en-US",
                "en",
                "en-us",
            },
            "ja-JP" => new[]
            {
                "ja-JP",
                "ja",
                "ja-jp",
            },
            _ => Array.Empty<string>(),
        };

        foreach (var key in keys)
        {
            if (titles.TryGetValue(key, out var value) &&
                !string.IsNullOrWhiteSpace(value))
            {
                return value;
            }
        }

        var prefix = language.Split('-')[0];
        foreach (var pair in titles)
        {
            if (pair.Key.StartsWith(
                    prefix,
                    StringComparison.OrdinalIgnoreCase) &&
                !string.IsNullOrWhiteSpace(pair.Value))
            {
                return pair.Value;
            }
        }

        return null;
    }

    private static bool IsDistinctTitle(
        string? candidate,
        string? primary) =>
        !string.IsNullOrWhiteSpace(candidate) &&
        !string.Equals(
            candidate,
            primary,
            StringComparison.CurrentCultureIgnoreCase);

    private static string FirstNonEmpty(
        params string?[] values) =>
        values.FirstOrDefault(
            static value =>
                !string.IsNullOrWhiteSpace(value))
        ?? string.Empty;
}
