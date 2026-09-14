using Eizo.Media;
using Eizo.Recognition;

namespace Eizo.MetadataIntegration;

public static class MediaModelProjection
{
    public static EizoMedia Project(
        MediaRecognitionSnapshot? recognition,
        MediaMetadataSnapshot? metadata,
        EizoMedia? existing = null)
    {
        var externalIds = MediaExternalIds.Merge(
            existing?.ExternalIds,
            metadata?.ExternalIds);

        if (metadata is
            {
                Provider.Length: > 0,
                ProviderSubjectId.Length: > 0,
            })
        {
            externalIds[
                MediaExternalIds.NormalizeProvider(metadata.Provider)] =
                metadata.ProviderSubjectId;
        }

        var format = ResolveFormat(recognition, metadata);
        if (format == MediaFormat.Unknown && existing is not null)
            format = existing.Format;

        var domain = ResolveDomain(metadata);
        if (domain == MediaContentDomain.Unknown && existing is not null)
            domain = existing.Domain;

        var origin = existing?.Origin ?? MediaOrigin.Unknown;
        var title =
            metadata?.CanonicalTitle ??
            metadata?.OriginalTitle ??
            recognition?.Title ??
            recognition?.LogicalPath;

        var id = existing is { Id.Length: > 0 }
            ? existing.Id
            : EizoMediaIdFactory.CreateInternal(
                title,
                recognition?.Year,
                format);

        return new EizoMedia(
            id,
            format,
            domain,
            origin,
            externalIds);
    }

    private static MediaFormat ResolveFormat(
        MediaRecognitionSnapshot? recognition,
        MediaMetadataSnapshot? metadata)
    {
        var specialKind = recognition?.SpecialKind;
        if (string.Equals(
                specialKind,
                "Ova",
                StringComparison.OrdinalIgnoreCase) ||
            string.Equals(
                specialKind,
                "Oad",
                StringComparison.OrdinalIgnoreCase))
        {
            return MediaFormat.Ova;
        }

        if (string.Equals(
                specialKind,
                "Ona",
                StringComparison.OrdinalIgnoreCase))
        {
            return MediaFormat.Ona;
        }

        if (string.Equals(
                metadata?.SubjectKind,
                "Movie",
                StringComparison.OrdinalIgnoreCase) ||
            string.Equals(
                recognition?.MediaKind,
                "Movie",
                StringComparison.OrdinalIgnoreCase))
        {
            return MediaFormat.Movie;
        }

        if (string.Equals(
                recognition?.MediaKind,
                "Special",
                StringComparison.OrdinalIgnoreCase))
        {
            return MediaFormat.Special;
        }

        if (string.Equals(
                metadata?.SubjectKind,
                "Series",
                StringComparison.OrdinalIgnoreCase) ||
            string.Equals(
                recognition?.MediaKind,
                "SeriesEpisode",
                StringComparison.OrdinalIgnoreCase))
        {
            return MediaFormat.TvSeries;
        }

        return MediaFormat.Unknown;
    }

    private static MediaContentDomain ResolveDomain(
        MediaMetadataSnapshot? metadata) =>
        metadata?.ContentKind?.Trim().ToLowerInvariant() switch
        {
            "animation" => MediaContentDomain.Animation,
            "liveaction" => MediaContentDomain.LiveAction,
            "documentary" => MediaContentDomain.Documentary,
            _ => MediaContentDomain.Unknown,
        };
}
