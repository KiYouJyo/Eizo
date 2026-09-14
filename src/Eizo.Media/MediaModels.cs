using System.Globalization;
using System.Security.Cryptography;
using System.Text;

namespace Eizo.Media;

public enum MediaFormat
{
    Unknown = 0,
    Movie = 1,
    TvSeries = 2,
    Ova = 3,
    Ona = 4,
    Special = 5,
}

public enum MediaContentDomain
{
    Unknown = 0,
    Animation = 1,
    LiveAction = 2,
    Documentary = 3,
}

public sealed record MediaOrigin(
    string? PrimaryCountryCode,
    List<string> CountryCodes)
{
    public static MediaOrigin Unknown => new(null, []);
}

public sealed record EizoMedia(
    string Id,
    MediaFormat Format,
    MediaContentDomain Domain,
    MediaOrigin Origin,
    Dictionary<string, string> ExternalIds)
{
    public string? GetExternalId(string provider)
    {
        var key = MediaExternalIds.NormalizeProvider(provider);
        return key.Length > 0 &&
               ExternalIds.TryGetValue(key, out var value) &&
               !string.IsNullOrWhiteSpace(value)
            ? value
            : null;
    }
}

public sealed record EizoSeries(
    EizoMedia Media,
    List<EizoSeason> Seasons);

public sealed record EizoSeason(
    string Id,
    int? Number,
    Dictionary<string, string> ExternalIds,
    List<EizoEpisode> Episodes);

public sealed record EizoEpisode(
    string Id,
    int? SeasonNumber,
    decimal? EpisodeNumber,
    bool IsSpecial,
    Dictionary<string, string> ExternalIds);

public static class MediaExternalIds
{
    public static Dictionary<string, string> Normalize(
        IEnumerable<KeyValuePair<string, string>>? values)
    {
        var result = new Dictionary<string, string>(
            StringComparer.OrdinalIgnoreCase);

        if (values is null)
            return result;

        foreach (var pair in values)
        {
            var provider = NormalizeProvider(pair.Key);
            var id = pair.Value?.Trim();
            if (provider.Length == 0 || string.IsNullOrWhiteSpace(id))
                continue;

            result[provider] = id;
        }

        return result;
    }

    public static Dictionary<string, string> Merge(
        params IEnumerable<KeyValuePair<string, string>>?[] sets)
    {
        var result = new Dictionary<string, string>(
            StringComparer.OrdinalIgnoreCase);

        foreach (var set in sets)
        {
            foreach (var pair in Normalize(set))
                result[pair.Key] = pair.Value;
        }

        return result;
    }

    public static string NormalizeProvider(string? provider)
    {
        var normalized = provider?.Trim().ToLowerInvariant() ?? string.Empty;
        return normalized switch
        {
            "bgm" => "bangumi",
            "ani-list" or "ani_list" => "anilist",
            "themoviedb" or "themoviedb.org" => "tmdb",
            _ => normalized,
        };
    }
}

public static class EizoMediaIdFactory
{
    private static readonly string[] PreferredExternalProviders =
    [
        "tmdb",
        "bangumi",
        "anilist",
        "imdb",
    ];

    public static string Create(
        IReadOnlyDictionary<string, string>? externalIds,
        string? title,
        int? year,
        MediaFormat format)
    {
        var normalizedIds = MediaExternalIds.Normalize(externalIds);
        foreach (var provider in PreferredExternalProviders)
        {
            if (normalizedIds.TryGetValue(provider, out var id))
                return $"eizo:{provider}:{id}";
        }

        var firstExternal = normalizedIds
            .OrderBy(static pair => pair.Key, StringComparer.Ordinal)
            .FirstOrDefault();
        if (!string.IsNullOrWhiteSpace(firstExternal.Key))
            return $"eizo:{firstExternal.Key}:{firstExternal.Value}";

        var normalizedTitle = NormalizeTitle(title);
        var seed = string.Create(
            CultureInfo.InvariantCulture,
            $"{(int)format}|{normalizedTitle}|{year?.ToString(CultureInfo.InvariantCulture) ?? "-"}");
        var hash = SHA256.HashData(Encoding.UTF8.GetBytes(seed));
        var suffix = Convert
            .ToHexString(hash.AsSpan(0, 12))
            .ToLowerInvariant();

        return $"eizo:local:{suffix}";
    }

    private static string NormalizeTitle(string? title)
    {
        var value = title?
            .Normalize(NormalizationForm.FormKC)
            .Trim()
            .ToUpperInvariant() ?? string.Empty;

        if (value.Length == 0)
            return "<unknown>";

        var builder = new StringBuilder(value.Length);
        foreach (var character in value)
        {
            if (char.IsLetterOrDigit(character))
                builder.Append(character);
        }

        return builder.Length == 0
            ? "<unknown>"
            : builder.ToString();
    }
}
