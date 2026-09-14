using Core = Eizo.Metadata.Core;

namespace Eizo.MetadataIntegration.Tests;

public sealed class MediaMetadataMergePolicyTests
{
    [Fact]
    public void AnimePrimaryKeepsBangumiIdentityAndFillsTmdbDetails()
    {
        var bangumiSubject = Subject(
            "bangumi",
            "400602",
            "葬送的芙莉莲",
            "Bangumi overview") with
        {
            ContentKind = Core.MetadataContentKind.Animation,
        };
        var bangumiEpisode = Episode(
            "bangumi-ep-1",
            bangumiSubject.Id,
            thumbnail: null) with
        {
            SeasonTitle = null,
        };

        var tmdbSubject = Subject(
            "tmdb",
            "209867",
            "Frieren: Beyond Journey's End",
            "TMDB overview") with
        {
            ContentKind = Core.MetadataContentKind.Animation,
            Genres = ["Animation", "Drama"],
            ProductionCompanies = ["Madhouse"],
            OriginCountryCodes = ["JP"],
            RuntimeMinutes = 25,
            Status = "Returning Series",
            OriginalLanguage = "ja",
            Cast =
            [
                new Core.MetadataPersonCredit(
                    "100",
                    "Atsumi Tanezaki",
                    "Frieren",
                    "Acting",
                    "https://image.tmdb.org/frieren.jpg",
                    0),
            ],
        };
        var tmdbEpisode = Episode(
            "62085",
            tmdbSubject.Id,
            "https://image.tmdb.org/pilot.jpg") with
        {
            ProviderSeasonId = "3572",
            SeasonTitle = "Season 1",
            SeasonPosterUrl =
                "https://image.tmdb.org/season1.jpg",
        };

        var merged = MediaMetadataMergePolicy.Merge(
            new MediaMetadataProviderSource(
                "bangumi",
                bangumiSubject,
                bangumiEpisode),
            [
                new MediaMetadataProviderSource(
                    "tmdb",
                    tmdbSubject,
                    tmdbEpisode),
            ]);

        Assert.Equal(
            "葬送的芙莉莲",
            merged.Subject.Titles.Primary);
        Assert.Equal(
            "Bangumi overview",
            merged.Subject.Overview);
        Assert.Equal(
            25,
            merged.Subject.RuntimeMinutes);
        Assert.Equal(
            "400602",
            merged.Subject.ExternalIds["bangumi"]);
        Assert.Equal(
            "209867",
            merged.Subject.ExternalIds["tmdb"]);
        Assert.Equal(
            "3572",
            merged.SeasonExternalIds["tmdb"]);
        Assert.Equal(
            "bangumi-ep-1",
            merged.EpisodeExternalIds["bangumi"]);
        Assert.Equal(
            "62085",
            merged.EpisodeExternalIds["tmdb"]);
        Assert.Equal(
            "https://image.tmdb.org/pilot.jpg",
            merged.Episode!.ThumbnailUrl);
        Assert.Equal(
            "bangumi",
            merged.FieldSources["CanonicalTitle"]);
        Assert.Equal(
            "bangumi",
            merged.FieldSources["Overview"]);
        Assert.Equal(
            "tmdb",
            merged.FieldSources["RuntimeMinutes"]);
        Assert.Equal(
            "tmdb",
            merged.FieldSources["EpisodeThumbnailUrl"]);
        Assert.Equal(
            ["bangumi", "tmdb"],
            merged.Contributors);
    }

    [Fact]
    public void AnimeProfilePrefersTmdbVisualsButBangumiText()
    {
        var bangumi = Subject(
            "bangumi",
            "400602",
            "葬送的芙莉莲",
            "Bangumi overview") with
        {
            ContentKind = Core.MetadataContentKind.Animation,
            Artwork = new Core.MetadataArtwork(
                "https://bgm.tv/poster.jpg",
                null,
                null),
        };
        var tmdb = Subject(
            "tmdb",
            "209867",
            "Frieren: Beyond Journey's End",
            "TMDB overview") with
        {
            ContentKind = Core.MetadataContentKind.Animation,
            Artwork = new Core.MetadataArtwork(
                "https://image.tmdb.org/poster.jpg",
                "https://image.tmdb.org/backdrop.jpg",
                null),
            RuntimeMinutes = 25,
        };

        var merged = MediaMetadataMergePolicy.Merge(
            new MediaMetadataProviderSource(
                "bangumi",
                bangumi,
                Episode(
                    "bgm-1",
                    bangumi.Id,
                    null)),
            [
                new MediaMetadataProviderSource(
                    "tmdb",
                    tmdb,
                    Episode(
                        "tmdb-1",
                        tmdb.Id,
                        "https://image.tmdb.org/still.jpg")),
            ]);

        Assert.Equal(
            MediaMetadataMergeProfile.Anime,
            merged.Profile);
        Assert.Equal(
            "葬送的芙莉莲",
            merged.Subject.Titles.Primary);
        Assert.Equal(
            "Bangumi overview",
            merged.Subject.Overview);
        Assert.Equal(
            "https://image.tmdb.org/poster.jpg",
            merged.Subject.Artwork.PosterUrl);
        Assert.Equal(
            "https://image.tmdb.org/backdrop.jpg",
            merged.Subject.Artwork.BackdropUrl);
        Assert.Equal(
            "https://image.tmdb.org/still.jpg",
            merged.Episode!.ThumbnailUrl);
        Assert.Equal(
            "tmdb",
            merged.FieldSources["PosterUrl"]);
        Assert.Equal(
            "tmdb",
            merged.FieldSources["EpisodeThumbnailUrl"]);
        Assert.Equal(
            "bangumi",
            merged.FieldSources["Overview"]);
    }

    [Fact]
    public void JapaneseLiveActionProfilePrefersTmdbDetailsAndVisuals()
    {
        var bangumi = Subject(
            "bangumi",
            "12345",
            "ドラゴン桜",
            "Bangumi overview") with
        {
            ContentKind = Core.MetadataContentKind.LiveAction,
            OriginalLanguage = "ja",
            OriginCountryCodes = ["JP"],
            Artwork = new Core.MetadataArtwork(
                "https://bgm.tv/dragon.jpg",
                null,
                null),
        };
        var tmdb = Subject(
            "tmdb",
            "55555",
            "Dragon Zakura",
            "TMDB overview") with
        {
            ContentKind = Core.MetadataContentKind.LiveAction,
            OriginalLanguage = "ja",
            OriginCountryCodes = ["JP"],
            RuntimeMinutes = 54,
            Artwork = new Core.MetadataArtwork(
                "https://image.tmdb.org/dragon.jpg",
                "https://image.tmdb.org/dragon-bg.jpg",
                null),
        };

        var merged = MediaMetadataMergePolicy.Merge(
            new MediaMetadataProviderSource(
                "tmdb",
                tmdb,
                null),
            [
                new MediaMetadataProviderSource(
                    "bangumi",
                    bangumi,
                    null),
            ]);

        Assert.Equal(
            MediaMetadataMergeProfile.JapaneseLiveAction,
            merged.Profile);
        Assert.Equal(
            "TMDB overview",
            merged.Subject.Overview);
        Assert.Equal(
            "https://image.tmdb.org/dragon.jpg",
            merged.Subject.Artwork.PosterUrl);
        Assert.Equal(
            54,
            merged.Subject.RuntimeMinutes);
    }

    [Fact]
    public void LiveActionPrimaryKeepsTmdbFieldsAndOnlyAddsBangumiIdentity()
    {
        var tmdb = Subject(
            "tmdb",
            "1396",
            "Breaking Bad",
            "TMDB overview") with
        {
            ContentKind = Core.MetadataContentKind.LiveAction,
            RuntimeMinutes = 47,
            OriginCountryCodes = ["US"],
        };
        var bangumi = Subject(
            "bangumi",
            "12345",
            "绝命毒师",
            "Bangumi overview") with
        {
            ContentKind = Core.MetadataContentKind.LiveAction,
        };

        var merged = MediaMetadataMergePolicy.Merge(
            new MediaMetadataProviderSource(
                "tmdb",
                tmdb,
                Episode(
                    "62085",
                    tmdb.Id,
                    "https://image.tmdb.org/still.jpg")),
            [
                new MediaMetadataProviderSource(
                    "bangumi",
                    bangumi,
                    null),
            ]);

        Assert.Equal(
            "Breaking Bad",
            merged.Subject.Titles.Primary);
        Assert.Equal(
            "TMDB overview",
            merged.Subject.Overview);
        Assert.Equal(
            47,
            merged.Subject.RuntimeMinutes);
        Assert.Equal(
            "1396",
            merged.Subject.ExternalIds["tmdb"]);
        Assert.Equal(
            "12345",
            merged.Subject.ExternalIds["bangumi"]);
        Assert.Equal(
            "tmdb",
            merged.FieldSources["CanonicalTitle"]);
        Assert.Equal(
            "tmdb",
            merged.FieldSources["Overview"]);
    }

    private static Core.MetadataSubject Subject(
        string provider,
        string id,
        string title,
        string overview) =>
        new(
            new Core.MetadataProviderItemId(
                provider,
                id,
                Core.MetadataSubjectKind.Series),
            new Core.MetadataTitles(
                title,
                title,
                new Dictionary<string, string>(),
                Array.Empty<string>()),
            overview,
            new DateOnly(2023, 1, 1),
            12,
            new Core.MetadataArtwork(
                null,
                null,
                null),
            new Dictionary<string, string>(
                StringComparer.OrdinalIgnoreCase)
            {
                [provider] = id,
            });

    private static Core.MetadataEpisode Episode(
        string providerEpisodeId,
        Core.MetadataProviderItemId subjectId,
        string? thumbnail) =>
        new(
            providerEpisodeId,
            subjectId,
            1,
            1,
            Core.MetadataEpisodeKind.Regular,
            new Core.MetadataTitles(
                "Episode 1",
                null,
                new Dictionary<string, string>(),
                Array.Empty<string>()),
            Overview: null,
            AirDate: new DateOnly(2023, 1, 1),
            ThumbnailUrl: thumbnail);
}
