using Eizo.PlaybackSupport;

namespace Eizo.Subtitle.Tests;

public sealed class ExternalSubtitleNameMatcherTests
{
    private const string Media =
        "[4K_NW] 黑礁 OVA 01【Bilibili_AYWDXNH】.mkv";

    [Theory]
    [InlineData("[4K_NW] 黑礁 OVA 01【Bilibili_AYWDXNH】.ass")]
    [InlineData("[4K_NW] 黑礁 OVA 01【Bilibili_AYWDXNH】.ja.srt")]
    [InlineData("[4K_NW] 黑礁 OVA 01【Bilibili_AYWDXNH】.zh-CN.ass")]
    [InlineData("[4K_NW] 黑礁 OVA 01【Bilibili_AYWDXNH】_en.vtt")]
    public void ExpectedFilenameFamilyMatches(string subtitle) =>
        Assert.True(ExternalSubtitleNameMatcher.IsMatch(subtitle, Media));

    [Fact]
    public void ZeroWidthCharactersDoNotBreakFilenameFamilyMatch()
    {
        const string subtitle =
            "[4K_NW] 黑礁 OVA 01【Bilibili_AYWDXNH】\u200B.ja.srt";

        Assert.True(
            ExternalSubtitleNameMatcher.IsMatch(
                subtitle,
                Media));
    }

    [Fact]
    public void UnrelatedEpisodeDoesNotMatch()
    {
        const string subtitle =
            "[4K_NW] 黑礁 OVA 02【Bilibili_AYWDXNH】.ja.srt";

        Assert.False(
            ExternalSubtitleNameMatcher.IsMatch(
                subtitle,
                Media));
    }

    [Fact]
    public void UnsupportedExtensionDoesNotMatch()
    {
        const string subtitle =
            "[4K_NW] 黑礁 OVA 01【Bilibili_AYWDXNH】.ja.txt";

        Assert.False(
            ExternalSubtitleNameMatcher.IsMatch(
                subtitle,
                Media));
    }
}
