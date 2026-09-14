using Eizo.Media;

namespace Eizo.Media.Tests;

public sealed class MediaModelTests
{
    [Fact]
    public void ExternalIdsNormalizeKnownProviderAliases()
    {
        var ids = MediaExternalIds.Normalize(
        [
            new("BGM", "253"),
            new("Ani-List", "154587"),
            new("TheMovieDB", "85937"),
        ]);

        Assert.Equal("253", ids["bangumi"]);
        Assert.Equal("154587", ids["anilist"]);
        Assert.Equal("85937", ids["tmdb"]);
    }

    [Fact]
    public void LocalMediaIdIsStableAcrossEpisodesOfSameRecognizedWork()
    {
        var left = EizoMediaIdFactory.Create(
            externalIds: null,
            title: "Breaking Bad",
            year: 2008,
            format: MediaFormat.TvSeries);
        var right = EizoMediaIdFactory.Create(
            externalIds: null,
            title: "Breaking Bad",
            year: 2008,
            format: MediaFormat.TvSeries);

        Assert.Equal(left, right);
        Assert.StartsWith("eizo:local:", left);
    }

    [Fact]
    public void SeriesHierarchyKeepsProviderIdsAtTheirOwnScope()
    {
        var media = new EizoMedia(
            "eizo:local:test",
            MediaFormat.TvSeries,
            MediaContentDomain.Animation,
            MediaOrigin.Unknown,
            new Dictionary<string, string>
            {
                ["tmdb"] = "100",
                ["bangumi"] = "200",
                ["anilist"] = "300",
            });

        var episode = new EizoEpisode(
            "episode-1",
            SeasonNumber: 1,
            EpisodeNumber: 1,
            IsSpecial: false,
            new Dictionary<string, string>
            {
                ["tmdb"] = "400",
            });
        var season = new EizoSeason(
            "season-1",
            Number: 1,
            new Dictionary<string, string>
            {
                ["tmdb"] = "500",
            },
            [episode]);
        var series = new EizoSeries(media, [season]);

        Assert.Equal("200", series.Media.GetExternalId("bangumi"));
        Assert.Equal("500", series.Seasons[0].ExternalIds["tmdb"]);
        Assert.Equal("400", series.Seasons[0].Episodes[0].ExternalIds["tmdb"]);
    }
}
