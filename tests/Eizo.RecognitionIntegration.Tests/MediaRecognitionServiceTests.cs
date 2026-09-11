using Eizo.Recognition;

namespace Eizo.RecognitionIntegration.Tests;

public sealed class MediaRecognitionServiceTests
{
    private readonly MediaRecognitionService _service = new();

    [Fact]
    public void Recognizes_ReZero_WithEpisodeTitle()
    {
        var result = _service.Recognize(
            "Re：从零开始的异世界生活 - S01E24 - 自称骑士与最优的骑士.mkv");

        Assert.Equal(MediaRecognitionStatus.Recognized, result.Status);
        Assert.Equal("Re：从零开始的异世界生活", result.Title);
        Assert.Equal("自称骑士与最优的骑士", result.EpisodeTitle);
        Assert.Equal(1, result.SeasonNumber);
        Assert.Equal(24m, result.EpisodeNumber);
        Assert.True(result.ShouldApplyDisplayTitle);
    }

    [Fact]
    public void Recognizes_AllBracketDeathNote()
    {
        var result = _service.Recognize(
            "[DBD-Raws][死亡笔记][34][1080P][BDRip][HEVC-10bit][FLAC].mkv");

        Assert.Equal("死亡笔记", result.Title);
        Assert.Equal(34m, result.EpisodeNumber);
        Assert.Equal("SeriesEpisode", result.MediaKind);
    }

    [Fact]
    public void Recognizes_GhostInTheShellArise_Remux()
    {
        var result = _service.Recognize(
            "攻殻機動隊ARISE border-4 Ghost Stands Alone.2014.1080p.BluRay.Remux.AVC.Dolby TrueHD.7.1 -WuKe.mkv");

        Assert.Equal("攻殻機動隊ARISE", result.Title);
        Assert.Equal("Ghost Stands Alone", result.EpisodeTitle);
        Assert.Equal(4m, result.EpisodeNumber);
        Assert.Equal(2014, result.Year);
    }

    [Fact]
    public void Recognizes_BilibiliTaggedBlackLagoon()
    {
        var result = _service.Recognize(
            "[4K_NW] 黑礁 22【Bilibili_AYWDXNH】.mkv");

        Assert.Equal("黑礁", result.Title);
        Assert.Equal(22m, result.EpisodeNumber);
    }

    [Fact]
    public void SlashStyles_ProduceSameSemanticResult()
    {
        var unix = _service.Recognize(
            "Anime/Season 01/Re：从零开始的异世界生活 - S01E24 - 自称骑士与最优的骑士.mkv");
        var windows = _service.Recognize(
            "Anime\\Season 01\\Re：从零开始的异世界生活 - S01E24 - 自称骑士与最优的骑士.mkv");

        Assert.Equal(unix.Title, windows.Title);
        Assert.Equal(unix.EpisodeTitle, windows.EpisodeTitle);
        Assert.Equal(unix.SeasonNumber, windows.SeasonNumber);
        Assert.Equal(unix.EpisodeNumber, windows.EpisodeNumber);
    }

    [Fact]
    public void AbsoluteWebDavUri_IsReducedToCredentialFreeLogicalPath()
    {
        var result = _service.Recognize(
            "https://user:secret@example.com/library/死亡笔记/[DBD-Raws][死亡笔记][34][1080P][BDRip][HEVC-10bit][FLAC].mkv");

        Assert.DoesNotContain("user", result.LogicalPath, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("secret", result.LogicalPath, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("example.com", result.LogicalPath, StringComparison.OrdinalIgnoreCase);

        foreach (var evidence in result.Evidence)
        {
            Assert.DoesNotContain("secret", evidence.Value ?? string.Empty, StringComparison.OrdinalIgnoreCase);
            Assert.DoesNotContain("example.com", evidence.Value ?? string.Empty, StringComparison.OrdinalIgnoreCase);
        }
    }

    [Fact]
    public void UnstructuredName_DoesNotThrow()
    {
        var result = _service.Recognize("misc/____.mkv");

        Assert.NotEqual(MediaRecognitionStatus.Error, result.Status);
    }

    [Fact]
    public void Snapshot_IsStampedWithActualRecognitionRuntimeVersion()
    {
        var result = _service.Recognize("Example.S01E01.mkv");

        Assert.False(string.IsNullOrWhiteSpace(result.RuntimeVersion));
        Assert.Equal(MediaRecognitionService.RuntimeVersion, result.RuntimeVersion);
    }

    [Fact]
    public void SameRequest_IsDeterministic()
    {
        const string path = "[DBD-Raws][死亡笔记][34][1080P][BDRip][HEVC-10bit][FLAC].mkv";

        var first = _service.Recognize(path);
        var second = _service.Recognize(path);

        Assert.Equal(first.LogicalPath, second.LogicalPath);
        Assert.Equal(first.Status, second.Status);
        Assert.Equal(first.MediaKind, second.MediaKind);
        Assert.Equal(first.Title, second.Title);
        Assert.Equal(first.EpisodeTitle, second.EpisodeTitle);
        Assert.Equal(first.SeasonNumber, second.SeasonNumber);
        Assert.Equal(first.EpisodeNumber, second.EpisodeNumber);
        Assert.Equal(first.Year, second.Year);
        Assert.Equal(first.Confidence, second.Confidence);
        Assert.Equal(first.ConfidenceLevel, second.ConfidenceLevel);
        Assert.Equal(first.IsAmbiguous, second.IsAmbiguous);
        Assert.Equal(first.TitleCandidates, second.TitleCandidates);
        Assert.Equal(first.Evidence, second.Evidence);
    }
}
