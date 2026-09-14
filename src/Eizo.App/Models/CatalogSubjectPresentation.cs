using Eizo.MetadataIntegration;

namespace Eizo.Models;

public sealed record CatalogSubjectPresentation(
    string Title,
    string SecondaryTitle,
    string Overview,
    string? PosterUrl,
    string? BackdropUrl,
    int? ReleaseYear,
    IReadOnlyList<string> Genres,
    int? RuntimeMinutes,
    IReadOnlyList<string> OriginCountryCodes,
    IReadOnlyList<string> ProductionCompanies,
    string? ProductionStatus,
    string? OriginalLanguage,
    int EpisodeCount,
    bool IsMovie,
    int SourceCount,
    bool HasLocalSource,
    bool HasRemoteSource)
{
    public static CatalogSubjectPresentation Create(
        CatalogSubjectModel subject)
    {
        ArgumentNullException.ThrowIfNull(subject);

        var metadata = subject.Metadata;
        var secondaryTitle =
            !string.IsNullOrWhiteSpace(subject.NativeTitle) &&
            !string.Equals(
                subject.NativeTitle,
                subject.Title,
                StringComparison.CurrentCultureIgnoreCase)
                ? subject.NativeTitle
                : string.Empty;

        int? releaseYear = null;
        if (metadata?.ReleaseDate is { Length: > 0 } release &&
            DateOnly.TryParse(release, out var date))
        {
            releaseYear = date.Year;
        }
        else
        {
            releaseYear = subject.Items
                .Select(static item =>
                    item.Recognition?.Year)
                .FirstOrDefault(static value =>
                    value is not null);
        }

        var sourceIds = subject.Items
            .Select(static item =>
                item.Location?.SourceId)
            .Where(static value =>
                !string.IsNullOrWhiteSpace(value))
            .Distinct(StringComparer.Ordinal)
            .Count();

        return new CatalogSubjectPresentation(
            subject.Title,
            secondaryTitle,
            metadata?.Overview ?? string.Empty,
            metadata?.PosterUrl,
            metadata?.BackdropUrl,
            releaseYear,
            metadata?.Genres.ToArray() ??
                Array.Empty<string>(),
            metadata?.RuntimeMinutes,
            metadata?.OriginCountryCodes.ToArray() ??
                Array.Empty<string>(),
            metadata?.ProductionCompanies.ToArray() ??
                Array.Empty<string>(),
            metadata?.ProductionStatus,
            metadata?.OriginalLanguage,
            subject.EpisodeCount,
            subject.IsMovieSubject,
            sourceIds,
            subject.Items.Any(static item =>
                item.Location?.Kind ==
                    MediaLocationKind.LocalFile),
            subject.Items.Any(static item =>
                item.Location?.Kind ==
                    MediaLocationKind.RemoteUri));
    }
}
