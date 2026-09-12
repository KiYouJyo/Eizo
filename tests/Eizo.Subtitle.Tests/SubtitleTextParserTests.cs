using Eizo.PlaybackSupport;

namespace Eizo.Subtitle.Tests;

public sealed class SubtitleTextParserTests
{
    [Fact]
    public void SrtParsesAndFindsCueAtPlaybackPosition()
    {
        const string text = """
            1
            00:00:01,000 --> 00:00:03,250
            第一行
            第二行

            2
            00:00:04,000 --> 00:00:05,000
            Next
            """;

        var document = SubtitleTextParser.Parse(".srt", text);

        Assert.Equal(2, document.Cues.Count);
        Assert.Equal(
            "第一行" + Environment.NewLine + "第二行",
            document.GetText(TimeSpan.FromSeconds(2)));
        Assert.Null(document.GetText(TimeSpan.FromSeconds(3.5)));
    }

    [Fact]
    public void VttParsesSettingsAndStripsCueMarkup()
    {
        const string text = """
            WEBVTT

            cue-1
            00:01.000 --> 00:03.000 align:start position:10%
            <b>Hello</b> <i>world</i>
            """;

        var document = SubtitleTextParser.Parse(".vtt", text);

        Assert.Single(document.Cues);
        Assert.Equal(
            "Hello world",
            document.GetText(TimeSpan.FromSeconds(2)));
    }

    [Fact]
    public void AssUsesDeclaredFormatAndRemovesOverrideTags()
    {
        const string text = """
            [Script Info]
            Title: sample

            [Events]
            Format: Layer, Start, End, Style, Name, MarginL, MarginR, MarginV, Effect, Text
            Dialogue: 0,0:00:01.20,0:00:04.50,Default,,0,0,0,,{\an8}上段\NSecond line
            """;

        var document = SubtitleTextParser.Parse(".ass", text);

        Assert.Single(document.Cues);
        Assert.Equal(
            "上段" + Environment.NewLine + "Second line",
            document.GetText(TimeSpan.FromSeconds(2)));
    }

    [Fact]
    public void OverlappingCuesAreCombinedInTimelineOrder()
    {
        const string text = """
            1
            00:00:01,000 --> 00:00:06,000
            A

            2
            00:00:02,000 --> 00:00:04,000
            B
            """;

        var document = SubtitleTextParser.Parse(".srt", text);

        Assert.Equal(
            "A" + Environment.NewLine + "B",
            document.GetText(TimeSpan.FromSeconds(3)));
    }
}
