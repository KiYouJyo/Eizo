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

public sealed record EizoHierarchyEpisodeSeed(
    string Key,
    int? SeasonNumber,
    decimal? EpisodeNumber,
    bool IsSpecial,
    Dictionary<string, string>? SeasonExternalIds = null,
    Dictionary<string, string>? EpisodeExternalIds = null);

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

    public static Dictionary<string, string> Common(
        params IEnumerable<KeyValuePair<string, string>>?[] sets)
    {
        var candidates = new Dictionary<string, HashSet<string>>(
            StringComparer.OrdinalIgnoreCase);

        foreach (var set in sets)
        {
            foreach (var pair in Normalize(set))
            {
                if (!candidates.TryGetValue(pair.Key, out var ids))
                {
                    ids = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
                    candidates.Add(pair.Key, ids);
                }

                ids.Add(pair.Value);
            }
        }

        var result = new Dictionary<string, string>(
            StringComparer.OrdinalIgnoreCase);
        foreach (var candidate in candidates)
        {
            if (candidate.Value.Count == 1)
                result[candidate.Key] = candidate.Value.Single();
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
    public static string CreateInternal(
        string? title,
        int? year,
        MediaFormat format)
    {
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


public static class EizoMediaHierarchy
{
    public static EizoSeries BuildSeries(
        EizoMedia media,
        IEnumerable<EizoHierarchyEpisodeSeed> episodeSeeds)
    {
        ArgumentNullException.ThrowIfNull(media);
        ArgumentNullException.ThrowIfNull(episodeSeeds);

        var indexed = episodeSeeds
            .Select((seed, index) => (Seed: seed, Index: index))
            .ToArray();

        var seasons = indexed
            .GroupBy(
                static item =>
                    item.Seed.SeasonNumber ??
                    (item.Seed.IsSpecial ? 0 : 1))
            .Select(group => BuildSeason(media, group.Key, group.ToArray()))
            .OrderBy(static season =>
                season.Number == 0
                    ? int.MaxValue
                    : season.Number ?? 1)
            .ToList();

        return new EizoSeries(media, seasons);
    }

    private static EizoSeason BuildSeason(
        EizoMedia media,
        int seasonNumber,
        IReadOnlyList<(EizoHierarchyEpisodeSeed Seed, int Index)> items)
    {
        var seasonId = $"{media.Id}:season:{seasonNumber}";
        var seasonExternalIds = MediaExternalIds.Common(
            items
                .Select(static item =>
                    (IEnumerable<KeyValuePair<string, string>>?)
                    item.Seed.SeasonExternalIds)
                .ToArray());

        var episodes = items
            .Select(item =>
            {
                var episodeKey = ResolveEpisodeKey(item.Seed, item.Index);
                return new EizoEpisode(
                    $"{seasonId}:episode:{episodeKey}",
                    seasonNumber,
                    item.Seed.EpisodeNumber,
                    item.Seed.IsSpecial,
                    MediaExternalIds.Normalize(
                        item.Seed.EpisodeExternalIds));
            })
            .OrderBy(static episode =>
                episode.EpisodeNumber ?? decimal.MaxValue)
            .ThenBy(static episode => episode.Id, StringComparer.Ordinal)
            .ToList();

        return new EizoSeason(
            seasonId,
            seasonNumber,
            seasonExternalIds,
            episodes);
    }

    private static string ResolveEpisodeKey(
        EizoHierarchyEpisodeSeed seed,
        int index)
    {
        if (seed.EpisodeNumber is { } number)
        {
            var prefix = seed.IsSpecial ? "sp" : "ep";
            return $"{prefix}-{number.ToString("0.###", CultureInfo.InvariantCulture)}";
        }

        var normalizedKey = seed.Key?.Trim() ?? string.Empty;
        if (normalizedKey.Length == 0)
            normalizedKey = index.ToString(CultureInfo.InvariantCulture);

        var hash = SHA256.HashData(Encoding.UTF8.GetBytes(normalizedKey));
        return $"item-{Convert.ToHexString(hash.AsSpan(0, 8)).ToLowerInvariant()}";
    }
}
