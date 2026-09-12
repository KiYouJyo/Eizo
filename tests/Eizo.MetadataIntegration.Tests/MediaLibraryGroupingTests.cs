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
    public void MixedResolvedAndUnresolvedEpisodesCollapseIntoResolvedSubject()
    {
        var assignments = MediaLibraryGrouping.AssignSubjectIdentities(
        [
            new(
                "ep1",
                Recognition("CLANNAD", 2007, 1),
                Metadata("bangumi", "51", "CLANNAD")),
            new(
                "ep2",
                Recognition("CLANNAD", 2007, 2),
                Metadata: null),
        ]);

        Assert.Equal(2, assignments.Count);
        Assert.NotNull(assignments[0].Identity);
        Assert.NotNull(assignments[1].Identity);
        Assert.Equal("metadata|bangumi|51", assignments[0].Identity!.Key);
        Assert.Equal(assignments[0].Identity, assignments[1].Identity);
    }

    [Fact]
    public void RecognitionFallbackDoesNotCrossMergeAmbiguousMetadataSubjects()
    {
        var assignments = MediaLibraryGrouping.AssignSubjectIdentities(
        [
            new(
                "resolved-a",
                Recognition("Example", 2024, 1),
                Metadata("bangumi", "100", "Example")),
            new(
                "resolved-b",
                Recognition("Example", 2024, 2),
                Metadata("bangumi", "200", "Example")),
            new(
                "unresolved",
                Recognition("Example", 2024, 3),
                Metadata: null),
        ]);

        Assert.Equal("metadata|bangumi|100", assignments[0].Identity!.Key);
        Assert.Equal("metadata|bangumi|200", assignments[1].Identity!.Key);
        Assert.Equal(
            "recognition|EXAMPLE|2024",
            assignments[2].Identity!.Key);
    }

    [Fact]
    public void RecognitionFallbackKeepsDifferentReleaseYearsSeparate()
    {
        var assignments = MediaLibraryGrouping.AssignSubjectIdentities(
        [
            new(
                "resolved",
                Recognition("Example", 2024, 1),
                Metadata("bangumi", "100", "Example")),
            new(
                "remake",
                Recognition("Example", 2025, 1),
                Metadata: null),
        ]);

        Assert.Equal("metadata|bangumi|100", assignments[0].Identity!.Key);
        Assert.Equal(
            "recognition|EXAMPLE|2025",
            assignments[1].Identity!.Key);
    }

    [Fact]
    public void DifferentMetadataSubjectsAcrossExplicitSeasonsCollapseIntoSeriesFamily()
    {
        var seasonOne = Recognition("进击的巨人", 2013, 1) with
        {
            SeasonNumber = 1,
        };
        var seasonTwo = Recognition("进击的巨人", 2017, 1) with
        {
            SeasonNumber = 2,
        };
        var seasonThree = Recognition("进击的巨人", 2018, 1) with
        {
            SeasonNumber = 3,
        };

        var assignments = MediaLibraryGrouping.AssignSubjectIdentities(
        [
            new(
                "s1",
                seasonOne,
                Metadata("bangumi", "55770", "进击的巨人")),
            new(
                "s2",
                seasonTwo,
                Metadata("bangumi", "118335", "进击的巨人 第二季")),
            new(
                "s3",
                seasonThree,
                Metadata("bangumi", "217300", "进击的巨人 第三季")),
        ]);

        Assert.Equal(3, assignments.Count);
        Assert.All(
            assignments,
            static assignment =>
            {
                Assert.NotNull(assignment.Identity);
                Assert.Equal(
                    "recognition-series-family",
                    assignment.Identity!.Basis);
                Assert.Equal(
                    "进击的巨人",
                    assignment.Identity.TitleHint);
            });

        Assert.Single(
            assignments
                .Select(static assignment => assignment.Identity!.Key)
                .Distinct(StringComparer.Ordinal));
    }

    [Fact]
    public void NumberedAnimeMoviesCollapseIntoOneFamilyCard()
    {
        var first = Recognition(
            "剧场版 空之境界 第一章 俯瞰风景",
            2007,
            null) with
        {
            MediaKind = "Movie",
        };
        var second = Recognition(
            "剧场版 空之境界 第二章 杀人考察（前）",
            2007,
            null) with
        {
            MediaKind = "Movie",
        };
        var epilogue = Recognition(
            "剧场版 空之境界 未来福音",
            2013,
            null) with
        {
            MediaKind = "Movie",
        };

        var assignments = MediaLibraryGrouping.AssignSubjectIdentities(
        [
            new("movie-1", first, Metadata: null),
            new("movie-2", second, Metadata: null),
            new("movie-extra", epilogue, Metadata: null),
        ]);

        Assert.Equal(3, assignments.Count);
        Assert.All(
            assignments,
            static assignment =>
            {
                Assert.NotNull(assignment.Identity);
                Assert.Equal(
                    "recognition-movie-family",
                    assignment.Identity!.Basis);
                Assert.Equal("空之境界", assignment.Identity.TitleHint);
            });
        Assert.Single(
            assignments
                .Select(static assignment => assignment.Identity!.Key)
                .Distinct(StringComparer.Ordinal));
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
    public void ResolvedMoviesGroupByMetadataSubject()
    {
        var recognition = Recognition("Movie", 2024, null) with
        {
            MediaKind = "Movie",
        };

        var identity = MediaLibraryGrouping.TryGetSubjectIdentity(
            recognition,
            Metadata("tmdb", "42", "Movie") with
            {
                SubjectKind = "Movie",
                ContentKind = "LiveAction",
            });

        Assert.NotNull(identity);
        Assert.Equal("metadata|tmdb|42", identity.Key);
        Assert.Equal("metadata-movie-subject", identity.Basis);
    }

    [Fact]
    public void AnimationContentHintClassifiesSeriesAndMoviesAsAnime()
    {
        var series = Recognition("葬送的芙莉莲", 2023, 1);
        var movie = Recognition("剧场版 空之境界 第一章 俯瞰风景", 2007, null) with
        {
            MediaKind = "Movie",
        };

        Assert.Equal(
            MediaLibraryCategoryHint.Anime,
            MediaLibraryGrouping.Classify(
                series,
                Metadata("bangumi", "400", "葬送的芙莉莲") with
                {
                    ContentKind = "Animation",
                }));
        Assert.Equal(
            MediaLibraryCategoryHint.Anime,
            MediaLibraryGrouping.Classify(
                movie,
                Metadata("bangumi", "500", "空之境界 第一章 俯瞰风景") with
                {
                    SubjectKind = "Movie",
                    ContentKind = "Animation",
                }));
    }

    [Fact]
    public void LiveActionContentHintSeparatesSeriesAndMovies()
    {
        Assert.Equal(
            MediaLibraryCategoryHint.Series,
            MediaLibraryGrouping.Classify(
                Recognition("ドラゴン桜", 2005, 1),
                Metadata("bangumi", "600", "龙樱") with
                {
                    ContentKind = "LiveAction",
                }));

        Assert.Equal(
            MediaLibraryCategoryHint.Movies,
            MediaLibraryGrouping.Classify(
                Recognition("飞驰人生2", 2024, null) with
                {
                    MediaKind = "Movie",
                },
                Metadata("tmdb", "700", "飞驰人生2") with
                {
                    SubjectKind = "Movie",
                    ContentKind = "LiveAction",
                }));
    }

    [Fact]
    public void UnresolvedTheatricalAnimeUsesRecognitionFallback()
    {
        var recognition = Recognition(
            "剧场版 空之境界 第二章 杀人考察（前）",
            2007,
            null) with
        {
            MediaKind = "Movie",
        };

        Assert.Equal(
            MediaLibraryCategoryHint.Anime,
            MediaLibraryGrouping.Classify(recognition, metadata: null));

        var identity = MediaLibraryGrouping.TryGetRecognitionSubjectIdentity(
            recognition);
        Assert.NotNull(identity);
        Assert.Equal("recognition-movie-title-year", identity.Basis);
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
