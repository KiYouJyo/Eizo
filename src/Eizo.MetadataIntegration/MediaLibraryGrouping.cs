using System.Text;
using Eizo.Recognition;

namespace Eizo.MetadataIntegration;

public sealed record MediaSubjectGroupingIdentity(
    string Key,
    string Basis);

public static class MediaLibraryGrouping
{
    public static MediaSubjectGroupingIdentity? TryGetSubjectIdentity(
        MediaRecognitionSnapshot recognition,
        MediaMetadataSnapshot? metadata)
    {
        ArgumentNullException.ThrowIfNull(recognition);

        if (recognition.Status != MediaRecognitionStatus.Recognized ||
            recognition.IsAmbiguous ||
            !IsSeriesLike(recognition.MediaKind))
        {
            return null;
        }

        if (metadata is
            {
                IsResolved: true,
                Provider.Length: > 0,
                ProviderSubjectId.Length: > 0,
                SubjectKind: "Series"
            })
        {
            return new MediaSubjectGroupingIdentity(
                $"metadata|{metadata.Provider!.Trim().ToLowerInvariant()}|{metadata.ProviderSubjectId}",
                "metadata-subject");
        }

        if (recognition.ConfidenceLevel is not ("High" or "Medium") ||
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
        return new MediaSubjectGroupingIdentity(
            $"recognition|{normalizedTitle}|{year}",
            "recognition-title-year");
    }

    private static bool IsSeriesLike(string mediaKind) =>
        string.Equals(
            mediaKind,
            "SeriesEpisode",
            StringComparison.OrdinalIgnoreCase) ||
        string.Equals(
            mediaKind,
            "Special",
            StringComparison.OrdinalIgnoreCase);

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
