using Eizo.Media;
using Eizo.Recognition;

namespace Eizo.MetadataIntegration.Tests;

public sealed class MediaModelProjectionTests
{
    [Fact]
    public void ProjectionCapturesProviderIdentityFormatAndDomain()
    {
        var media = MediaModelProjection.Project(
            Recognition("氷菓", 2012),
            Metadata(
                provider: "bangumi",
                id: "27364",
                contentKind: "Animation",
                externalIds: new Dictionary<string, string>
                {
                    ["anilist"] = "12189",
                }));

        Assert.Equal(MediaFormat.TvSeries, media.Format);
        Assert.Equal(MediaContentDomain.Animation, media.Domain);
        Assert.Equal("27364", media.GetExternalId("bangumi"));
        Assert.Equal("12189", media.GetExternalId("anilist"));
    }

    [Fact]
    public void ProjectionStartingWithMetadataKeepsProviderOutOfInternalId()
    {
        var media = MediaModelProjection.Project(
            Recognition("Breaking Bad", 2008),
            Metadata(
                provider: "tmdb",
                id: "1396",
                contentKind: "LiveAction",
                externalIds: new Dictionary<string, string>
                {
                    ["imdb"] = "tt0903747",
                }));

        Assert.StartsWith("eizo:local:", media.Id);
        Assert.DoesNotContain("1396", media.Id);
        Assert.Equal("1396", media.GetExternalId("tmdb"));
    }

    [Fact]
    public void ProjectionPreservesEizoIdWhenNewProviderIdsArrive()
    {
        var recognition = Recognition("Breaking Bad", 2008);
        var initial = MediaModelProjection.Project(recognition, metadata: null);

        var enriched = MediaModelProjection.Project(
            recognition,
            Metadata(
                provider: "tmdb",
                id: "1396",
                contentKind: "LiveAction",
                externalIds: new Dictionary<string, string>
                {
                    ["imdb"] = "tt0903747",
                }),
            initial);

        Assert.Equal(initial.Id, enriched.Id);
        Assert.Equal("1396", enriched.GetExternalId("tmdb"));
        Assert.Equal("tt0903747", enriched.GetExternalId("imdb"));
        Assert.Equal(MediaContentDomain.LiveAction, enriched.Domain);
    }

    [Fact]
    public void EpisodeExternalIdsUseExactProviderEpisodeIdentity()
    {
        var metadata = Metadata(
            provider: "tmdb",
            id: "1396",
            contentKind: "LiveAction",
            externalIds: new Dictionary<string, string>
            {
                ["tmdb"] = "1396",
            }) with
        {
            EpisodeSeasonNumber = 1,
            ProviderEpisodeId = "62085",
        };

        var ids = MediaModelProjection.ProjectEpisodeExternalIds(metadata);

        Assert.Equal("62085", ids["tmdb"]);
        Assert.DoesNotContain("1396", ids.Values);
    }

    [Fact]
    public void ProjectionDistinguishesMovieAndOvaFormats()
    {
        var movie = Recognition("Oppenheimer", 2023) with
        {
            MediaKind = "Movie",
        };
        var ova = Recognition("BLACK LAGOON Roberta's Blood Trail", 2010) with
        {
            MediaKind = "Special",
            SpecialKind = "Ova",
        };

        Assert.Equal(
            MediaFormat.Movie,
            MediaModelProjection.Project(movie, metadata: null).Format);
        Assert.Equal(
            MediaFormat.Ova,
            MediaModelProjection.Project(ova, metadata: null).Format);
    }

    private static MediaRecognitionSnapshot Recognition(
        string title,
        int? year) =>
        new(
            LogicalPath: $"{title}.S01E01.mkv",
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
            EpisodeNumber: 1,
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
        string contentKind,
        Dictionary<string, string> externalIds) =>
        new(
            RuntimeVersion: "0.2.20",
            RecognitionRuntimeVersion: "0.1.6",
            Status: MediaMetadataStatus.Resolved,
            Provider: provider,
            ProviderSubjectId: id,
            SubjectKind: "Series",
            CanonicalTitle: "Title",
            OriginalTitle: "Title",
            LocalizedTitles: new Dictionary<string, string>(),
            Aliases: [],
            Overview: null,
            ReleaseDate: null,
            EpisodeCount: null,
            PosterUrl: null,
            BackdropUrl: null,
            ExternalIds: externalIds,
            EpisodeNumber: 1,
            EpisodeTitle: null,
            EpisodeOriginalTitle: null,
            EpisodeOverview: null,
            EpisodeAirDate: null,
            EpisodeThumbnailUrl: null,
            Confidence: 0.95,
            Errors: [],
            UpdatedAtUtc: DateTimeOffset.UtcNow)
        {
            ContentKind = contentKind,
        };
}
