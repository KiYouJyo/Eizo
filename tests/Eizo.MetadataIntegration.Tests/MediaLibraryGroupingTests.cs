using Eizo.MetadataIntegration;
using Eizo.Recognition;

namespace Eizo.MetadataIntegration.Tests;

public sealed class MediaLibraryGroupingTests
{
    [Fact]
    public void MetadataSubjectIdOverridesCrossLanguageRecognitionTitles()
    {
        var metadataA = Metadata("bangumi", "253", "攻壳机动队");
        var metadataB = Metadata("bangumi", "253", "Ghost in the Shell");

        var left = MediaLibraryGrouping.TryGetSubjectIdentity(
            Recognition("攻殻機動隊", 2002, 1),
            metadataA);
        var right = MediaLibraryGrouping.TryGetSubjectIdentity(
            Recognition("Ghost in the Shell", 2002, 2),
            metadataB);

        Assert.NotNull(left);
        Assert.NotNull(right);
        Assert.Equal(left.Key, right.Key);
        Assert.Equal("metadata-subject", left.Basis);
    }

    [Fact]
    public void RecognitionFallbackGroupsEpisodesOfSameReliableTitle()
    {
        var left = MediaLibraryGrouping.TryGetSubjectIdentity(
            Recognition("CLANNAD", 2007, 1),
            metadata: null);
        var right = MediaLibraryGrouping.TryGetSubjectIdentity(
            Recognition("CLANNAD", 2007, 18),
            metadata: null);

        Assert.NotNull(left);
        Assert.Equal(left, right);
        Assert.Equal("recognition-title-year", left.Basis);
    }

    [Fact]
    public void AmbiguousItemsRemainStandalone()
    {
        var recognition = Recognition("Conflicting Show", 2020, 1) with
        {
            Status = MediaRecognitionStatus.Ambiguous,
            IsAmbiguous = true,
        };

        Assert.Null(MediaLibraryGrouping.TryGetSubjectIdentity(
            recognition,
            metadata: null));
    }

    [Fact]
    public void MoviesRemainStandalone()
    {
        var recognition = Recognition("Movie", 2024, null) with
        {
            MediaKind = "Movie",
        };

        Assert.Null(MediaLibraryGrouping.TryGetSubjectIdentity(
            recognition,
            Metadata("tmdb", "42", "Movie") with
            {
                SubjectKind = "Movie",
            }));
    }

    private static MediaRecognitionSnapshot Recognition(
        string title,
        int? year,
        decimal? episode) =>
        new(
            LogicalPath: $"{title} - {episode}.mkv",
            Status: MediaRecognitionStatus.Recognized,
            MediaKind: "SeriesEpisode",
            SpecialKind: "None",
            EpisodePart: "None",
            IsFinalEpisode: false,
            Title: title,
            EpisodeTitle: null,
            TitleCandidates: [],
            SeasonNumber: 1,
            CourNumber: null,
            EpisodeNumber: episode,
            EpisodeEndNumber: null,
            SpecialNumber: null,
            Year: year,
            Confidence: 0.95,
            ConfidenceLevel: "High",
            IsAmbiguous: false,
            Evidence: [])
        {
            RuntimeVersion = "0.1.6",
        };

    private static MediaMetadataSnapshot Metadata(
        string provider,
        string id,
        string title) =>
        new(
            RuntimeVersion: "0.2.1",
            RecognitionRuntimeVersion: "0.1.6",
            Status: MediaMetadataStatus.Resolved,
            Provider: provider,
            ProviderSubjectId: id,
            SubjectKind: "Series",
            CanonicalTitle: title,
            OriginalTitle: title,
            LocalizedTitles: new Dictionary<string, string>(),
            Aliases: [],
            Overview: null,
            ReleaseDate: null,
            EpisodeCount: null,
            PosterUrl: null,
            BackdropUrl: null,
            ExternalIds: new Dictionary<string, string>(),
            EpisodeNumber: null,
            EpisodeTitle: null,
            EpisodeOriginalTitle: null,
            EpisodeOverview: null,
            EpisodeAirDate: null,
            EpisodeThumbnailUrl: null,
            Confidence: 0.95,
            Errors: [],
            UpdatedAtUtc: DateTimeOffset.UtcNow);
}
