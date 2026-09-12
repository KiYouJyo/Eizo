using System.Text;
using System.Text.RegularExpressions;
using Eizo.Recognition;

namespace Eizo.MetadataIntegration;

public sealed record MediaSubjectGroupingIdentity(
    string Key,
    string Basis)
{
    public string? TitleHint { get; init; }
}

public enum MediaLibraryCategoryHint
{
    Unknown = 0,
    Anime = 1,
    Series = 2,
    Movies = 3,
}

public sealed record MediaLibraryGroupingInput(
    string ItemKey,
    MediaRecognitionSnapshot? Recognition,
    MediaMetadataSnapshot? Metadata);

public sealed record MediaLibraryGroupingAssignment(
    string ItemKey,
    MediaSubjectGroupingIdentity? Identity);

public static class MediaLibraryGrouping
{
    public static MediaSubjectGroupingIdentity? TryGetSubjectIdentity(
        MediaRecognitionSnapshot recognition,
        MediaMetadataSnapshot? metadata)
    {
        ArgumentNullException.ThrowIfNull(recognition);

        return TryGetMetadataSubjectIdentity(recognition, metadata)
            ?? TryGetRecognitionSubjectIdentity(recognition);
    }

    public static IReadOnlyList<MediaLibraryGroupingAssignment> AssignSubjectIdentities(
        IReadOnlyList<MediaLibraryGroupingInput> items)
    {
        ArgumentNullException.ThrowIfNull(items);

        var metadataIdentities = new Dictionary<string, MediaSubjectGroupingIdentity>(
            StringComparer.Ordinal);
        var recognitionIdentities = new Dictionary<string, MediaSubjectGroupingIdentity>(
            StringComparer.Ordinal);
        var metadataOwnersByRecognitionKey = new Dictionary<string, HashSet<string>>(
            StringComparer.Ordinal);
        var movieFamilyIdentities = BuildMovieFamilyIdentities(items);

        foreach (var item in items)
        {
            if (item.Recognition is null)
            {
                continue;
            }

            var metadataIdentity = TryGetMetadataSubjectIdentity(
                item.Recognition,
                item.Metadata);
            var recognitionIdentity = TryGetRecognitionSubjectIdentity(
                item.Recognition);

            if (metadataIdentity is not null)
            {
                metadataIdentities[item.ItemKey] = metadataIdentity;

                if (recognitionIdentity is not null)
                {
                    if (!metadataOwnersByRecognitionKey.TryGetValue(
                            recognitionIdentity.Key,
                            out var owners))
                    {
                        owners = new HashSet<string>(StringComparer.Ordinal);
                        metadataOwnersByRecognitionKey.Add(
                            recognitionIdentity.Key,
                            owners);
                    }

                    owners.Add(metadataIdentity.Key);
                }
            }

            if (recognitionIdentity is not null)
            {
                recognitionIdentities[item.ItemKey] = recognitionIdentity;
            }
        }

        var canonicalMetadataIdentities = metadataIdentities.Values
            .GroupBy(static identity => identity.Key, StringComparer.Ordinal)
            .ToDictionary(
                static group => group.Key,
                static group => group.First(),
                StringComparer.Ordinal);

        var result = new List<MediaLibraryGroupingAssignment>(items.Count);

        foreach (var item in items)
        {
            if (movieFamilyIdentities.TryGetValue(
                    item.ItemKey,
                    out var movieFamilyIdentity))
            {
                result.Add(new MediaLibraryGroupingAssignment(
                    item.ItemKey,
                    movieFamilyIdentity));
                continue;
            }

            if (metadataIdentities.TryGetValue(item.ItemKey, out var metadataIdentity))
            {
                result.Add(new MediaLibraryGroupingAssignment(
                    item.ItemKey,
                    metadataIdentity));
                continue;
            }

            if (!recognitionIdentities.TryGetValue(
                    item.ItemKey,
                    out var recognitionIdentity))
            {
                result.Add(new MediaLibraryGroupingAssignment(
                    item.ItemKey,
                    Identity: null));
                continue;
            }

            if (metadataOwnersByRecognitionKey.TryGetValue(
                    recognitionIdentity.Key,
                    out var owners) &&
                owners.Count == 1)
            {
                var metadataKey = owners.Single();
                if (canonicalMetadataIdentities.TryGetValue(
                        metadataKey,
                        out var reconciledIdentity))
                {
                    result.Add(new MediaLibraryGroupingAssignment(
                        item.ItemKey,
                        reconciledIdentity));
                    continue;
                }
            }

            result.Add(new MediaLibraryGroupingAssignment(
                item.ItemKey,
                recognitionIdentity));
        }

        return result;
    }

    private static readonly Regex LeadingMovieMarkerRegex = new(
        @"^\s*(?:(?:劇場版|剧场版|映画版|映画)|(?:THE\s+)?MOVIE)\s*[:：._\-–—]?\s*",
        RegexOptions.IgnoreCase | RegexOptions.CultureInvariant,
        TimeSpan.FromMilliseconds(50));

    private static readonly Regex MovieInstallmentRegex = new(
        @"^(?<family>.+?)(?:\s+|[-_:：])(?:(?:第\s*[一二三四五六七八九十两兩〇零壹贰貳叁參肆伍陆陸柒捌玖拾\d]{1,3}\s*(?:章|部|篇|幕|話|话|夜))|(?:(?:CHAPTER|BORDER)\s*[-_:：]?\s*\d{1,2}))",
        RegexOptions.IgnoreCase | RegexOptions.CultureInvariant,
        TimeSpan.FromMilliseconds(50));

    private static IReadOnlyDictionary<string, MediaSubjectGroupingIdentity>
        BuildMovieFamilyIdentities(
            IReadOnlyList<MediaLibraryGroupingInput> items)
    {
        var candidates = items
            .Where(static item =>
                item.Recognition is
                {
                    Status: MediaRecognitionStatus.Recognized,
                    IsAmbiguous: false,
                    ConfidenceLevel: "High" or "Medium",
                    Title.Length: > 0,
                    MediaKind: "Movie",
                })
            .Where(item =>
                Classify(item.Recognition, item.Metadata) ==
                MediaLibraryCategoryHint.Anime)
            .Select(item =>
            {
                var title = CleanMovieTitle(item.Recognition!.Title!);
                return new MovieFamilyCandidate(
                    item.ItemKey,
                    title,
                    NormalizeTitle(title),
                    TryExtractMovieFamilyTitle(title));
            })
            .Where(static item => item.NormalizedTitle.Length >= 3)
            .ToArray();

        var seeds = candidates
            .Where(static item => !string.IsNullOrWhiteSpace(item.FamilyTitle))
            .GroupBy(
                static item => NormalizeTitle(item.FamilyTitle!),
                StringComparer.Ordinal)
            .Where(static group =>
                group.Key.Length >= 3 &&
                group.Select(static item => item.NormalizedTitle)
                    .Distinct(StringComparer.Ordinal)
                    .Count() >= 2)
            .Select(static group => new MovieFamilySeed(
                group.Key,
                group.Select(static item => item.FamilyTitle!)
                    .OrderBy(static value => value.Length)
                    .First()))
            .OrderByDescending(static seed => seed.NormalizedTitle.Length)
            .ToArray();

        if (seeds.Length == 0)
        {
            return new Dictionary<string, MediaSubjectGroupingIdentity>(
                StringComparer.Ordinal);
        }

        var result = new Dictionary<string, MediaSubjectGroupingIdentity>(
            StringComparer.Ordinal);

        foreach (var candidate in candidates)
        {
            var seed = seeds.FirstOrDefault(value =>
                candidate.NormalizedTitle.StartsWith(
                    value.NormalizedTitle,
                    StringComparison.Ordinal));
            if (seed is null)
            {
                continue;
            }

            result[candidate.ItemKey] = new MediaSubjectGroupingIdentity(
                $"recognition-movie-family|{seed.NormalizedTitle}",
                "recognition-movie-family")
            {
                TitleHint = seed.DisplayTitle,
            };
        }

        return result;
    }

    private static string CleanMovieTitle(string value)
    {
        var normalized = value
            .Normalize(NormalizationForm.FormKC)
            .Trim();

        try
        {
            normalized = LeadingMovieMarkerRegex.Replace(
                normalized,
                string.Empty);
        }
        catch (RegexMatchTimeoutException)
        {
        }

        return normalized.Trim();
    }

    private static string? TryExtractMovieFamilyTitle(string value)
    {
        try
        {
            var match = MovieInstallmentRegex.Match(value);
            if (!match.Success)
            {
                return null;
            }

            var family = match.Groups["family"].Value
                .Trim(' ', '-', '–', '—', '_', '.', ':', '：');
            return family.Length >= 2 ? family : null;
        }
        catch (RegexMatchTimeoutException)
        {
            return null;
        }
    }

    private sealed record MovieFamilyCandidate(
        string ItemKey,
        string DisplayTitle,
        string NormalizedTitle,
        string? FamilyTitle);

    private sealed record MovieFamilySeed(
        string NormalizedTitle,
        string DisplayTitle);

    public static MediaSubjectGroupingIdentity? TryGetMetadataSubjectIdentity(
        MediaRecognitionSnapshot recognition,
        MediaMetadataSnapshot? metadata)
    {
        ArgumentNullException.ThrowIfNull(recognition);

        if (!IsEligibleRecognition(recognition))
        {
            return null;
        }

        if (metadata is not
            {
                IsResolved: true,
                Provider.Length: > 0,
                ProviderSubjectId.Length: > 0
            } ||
            metadata.SubjectKind is not ("Series" or "Movie"))
        {
            return null;
        }

        return new MediaSubjectGroupingIdentity(
            $"metadata|{metadata.Provider!.Trim().ToLowerInvariant()}|{metadata.ProviderSubjectId}",
            metadata.SubjectKind == "Movie"
                ? "metadata-movie-subject"
                : "metadata-subject");
    }

    public static MediaSubjectGroupingIdentity? TryGetRecognitionSubjectIdentity(
        MediaRecognitionSnapshot recognition)
    {
        ArgumentNullException.ThrowIfNull(recognition);

        if (!IsEligibleRecognition(recognition) ||
            recognition.ConfidenceLevel is not ("High" or "Medium") ||
            string.IsNullOrWhiteSpace(recognition.Title))
        {
            return null;
        }

        var normalizedTitle = NormalizeTitle(recognition.Title);
        if (normalizedTitle.Length == 0)
        {
            return null;
        }

        var year = recognition.Year?.ToString() ?? "-";
        var movie = string.Equals(
            recognition.MediaKind,
            "Movie",
            StringComparison.OrdinalIgnoreCase);

        return new MediaSubjectGroupingIdentity(
            movie
                ? $"recognition-movie|{normalizedTitle}|{year}"
                : $"recognition|{normalizedTitle}|{year}",
            movie
                ? "recognition-movie-title-year"
                : "recognition-title-year");
    }

    public static MediaLibraryCategoryHint Classify(
        MediaRecognitionSnapshot? recognition,
        MediaMetadataSnapshot? metadata)
    {
        var contentKind = metadata?.ContentKind;
        if (string.Equals(
                contentKind,
                "Animation",
                StringComparison.OrdinalIgnoreCase))
        {
            return MediaLibraryCategoryHint.Anime;
        }

        if (string.Equals(
                contentKind,
                "LiveAction",
                StringComparison.OrdinalIgnoreCase))
        {
            return string.Equals(
                    metadata?.SubjectKind,
                    "Movie",
                    StringComparison.OrdinalIgnoreCase)
                ? MediaLibraryCategoryHint.Movies
                : MediaLibraryCategoryHint.Series;
        }

        if (recognition is null ||
            recognition.Status != MediaRecognitionStatus.Recognized ||
            recognition.IsAmbiguous)
        {
            return MediaLibraryCategoryHint.Unknown;
        }

        if (LooksLikeAnimeRelease(recognition))
        {
            return MediaLibraryCategoryHint.Anime;
        }

        if (string.Equals(
                recognition.MediaKind,
                "Movie",
                StringComparison.OrdinalIgnoreCase))
        {
            return MediaLibraryCategoryHint.Movies;
        }

        if (string.Equals(
                recognition.MediaKind,
                "SeriesEpisode",
                StringComparison.OrdinalIgnoreCase) ||
            string.Equals(
                recognition.MediaKind,
                "Special",
                StringComparison.OrdinalIgnoreCase))
        {
            return MediaLibraryCategoryHint.Series;
        }

        return MediaLibraryCategoryHint.Unknown;
    }

    private static bool IsEligibleRecognition(
        MediaRecognitionSnapshot recognition) =>
        recognition.Status == MediaRecognitionStatus.Recognized &&
        !recognition.IsAmbiguous &&
        (string.Equals(
             recognition.MediaKind,
             "SeriesEpisode",
             StringComparison.OrdinalIgnoreCase) ||
         string.Equals(
             recognition.MediaKind,
             "Special",
             StringComparison.OrdinalIgnoreCase) ||
         string.Equals(
             recognition.MediaKind,
             "Movie",
             StringComparison.OrdinalIgnoreCase));

    private static bool LooksLikeAnimeRelease(
        MediaRecognitionSnapshot recognition)
    {
        if (recognition.SpecialKind is
            "Ova" or "Oad" or "NcOp" or "NcEd")
        {
            return true;
        }

        var value = string.Join(
            " ",
            recognition.Title ?? string.Empty,
            recognition.LogicalPath);

        string[] releaseMarkers =
        [
            "VCB-STUDIO",
            "DBD-RAWS",
            "ANIME",
            "NCOP",
            "NCED",
            "OVA",
            "OAD",
        ];

        var normalized = value
            .Normalize(NormalizationForm.FormKC)
            .ToUpperInvariant();

        if (releaseMarkers.Any(marker =>
                normalized.Contains(
                    marker,
                    StringComparison.OrdinalIgnoreCase)))
        {
            return true;
        }

        if (!string.Equals(
                recognition.MediaKind,
                "Movie",
                StringComparison.OrdinalIgnoreCase))
        {
            return false;
        }

        string[] theatricalMarkers =
        [
            "劇場版",
            "剧场版",
        ];

        return theatricalMarkers.Any(marker =>
            normalized.Contains(
                marker,
                StringComparison.OrdinalIgnoreCase));
    }

    private static string NormalizeTitle(string value)
    {
        var normalized = value
            .Normalize(NormalizationForm.FormKC)
            .ToUpperInvariant();

        var builder = new StringBuilder(normalized.Length);
        foreach (var c in normalized)
        {
            if (char.IsLetterOrDigit(c))
            {
                builder.Append(c);
            }
        }

        return builder.ToString();
    }
}
