using Eizo.Models;

namespace Eizo.Subtitle.Tests;

public sealed class CatalogEpisodeGroupingResolverTests
{
    [Fact]
    public void StaleRegularSnapshotCannotCollapseExplicitOvaIntoS01E01()
    {
        var identity = CatalogEpisodeGroupingResolver.Resolve(
            "[4K_NW] 黑礁 OVA 01【Bilibili_AYWDXNH】",
            mediaKind: "SeriesEpisode",
            specialKind: "None",
            seasonNumber: 1,
            episodeNumber: 1m,
            specialNumber: null);

        Assert.True(identity.IsSpecial);
        Assert.Equal(0, identity.SeasonNumber);
        Assert.Equal(1m, identity.EpisodeNumber);
    }

    [Fact]
    public void ExplicitOvaMarkerWinsOverStaleSeasonNumber()
    {
        var identity = CatalogEpisodeGroupingResolver.Resolve(
            "[4K_NW] 黑礁 OVA 05【Bilibili_AYWDXNH】",
            mediaKind: "Special",
            specialKind: "Ova",
            seasonNumber: 1,
            episodeNumber: null,
            specialNumber: 5m);

        Assert.True(identity.IsSpecial);
        Assert.Equal(0, identity.SeasonNumber);
        Assert.Equal(5m, identity.EpisodeNumber);
    }

    [Fact]
    public void RegularEpisodeRemainsSeasonOneEpisodeOne()
    {
        var identity = CatalogEpisodeGroupingResolver.Resolve(
            "[4K_NW] 黑礁 01【Bilibili_AYWDXNH】",
            mediaKind: "SeriesEpisode",
            specialKind: "None",
            seasonNumber: null,
            episodeNumber: 1m,
            specialNumber: null);

        Assert.False(identity.IsSpecial);
        Assert.Equal(1, identity.SeasonNumber);
        Assert.Equal(1m, identity.EpisodeNumber);
    }

    [Theory]
    [InlineData("Show OAD 02", 2)]
    [InlineData("Show ONA 03", 3)]
    [InlineData("Show SP 04", 4)]
    [InlineData("Show SPECIAL 05", 5)]
    public void ExplicitSpecialFamiliesUseSeasonZero(
        string sourceTitle,
        int expectedNumber)
    {
        var identity = CatalogEpisodeGroupingResolver.Resolve(
            sourceTitle,
            mediaKind: "SeriesEpisode",
            specialKind: "None",
            seasonNumber: 1,
            episodeNumber: expectedNumber,
            specialNumber: null);

        Assert.True(identity.IsSpecial);
        Assert.Equal(0, identity.SeasonNumber);
        Assert.Equal((decimal)expectedNumber, identity.EpisodeNumber);
    }

    [Fact]
    public void ExplicitOvaSourceBuildsStableSpecialLabel()
    {
        var label =
            CatalogEpisodeGroupingResolver.ResolveExplicitSpecialLabel(
                "[4K_NW] 黑礁 OVA 01【Bilibili_AYWDXNH】",
                fallbackNumber: null);

        Assert.Equal("OVA 1", label);
    }

    [Fact]
    public void ExplicitSpecialSourceDetectionRejectsRegularEpisode()
    {
        Assert.True(
            CatalogEpisodeGroupingResolver.IsExplicitSpecialSource(
                "[4K_NW] 黑礁 OVA 01【Bilibili_AYWDXNH】"));

        Assert.False(
            CatalogEpisodeGroupingResolver.IsExplicitSpecialSource(
                "[4K_NW] 黑礁 01【Bilibili_AYWDXNH】"));
    }

}
