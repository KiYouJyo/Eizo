using System.Text;
using Eizo.Recognition;

namespace Eizo.MetadataIntegration;

public sealed record MediaSubjectGroupingIdentity(
    string Key,
    string Basis);

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

        string[] markers =
        [
            "VCB-STUDIO",
            "DBD-RAWS",
            "ANIME",
            "BDRIP",
            "NCOP",
            "NCED",
            "OVA",
            "OAD",
            "劇場版",
            "剧场版",
            "映画",
            "ANIPLEX",
        ];

        var normalized = value
            .Normalize(NormalizationForm.FormKC)
            .ToUpperInvariant();

        return markers.Any(marker =>
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
