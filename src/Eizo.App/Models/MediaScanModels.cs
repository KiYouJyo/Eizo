namespace Eizo.Models;

public enum MediaScanStatus
{
    Running = 0,
    Completed = 1,
    Failed = 2,
    Canceled = 3
}

public sealed record MediaScanProgress(
    string SourceId,
    int DirectoriesProcessed,
    int DirectoriesPending,
    int VideosDiscovered,
    string? CurrentPath = null);

public sealed record MediaScanSnapshot(
    string SourceId,
    MediaScanStatus Status,
    int DirectoriesProcessed,
    int DirectoriesPending,
    int VideosDiscovered,
    string? CurrentPath,
    string? ErrorCode,
    string? ErrorDetail,
    DateTimeOffset StartedUtc,
    DateTimeOffset? FinishedUtc = null)
{
    public int KnownDirectoryTotal =>
        DirectoriesProcessed + DirectoriesPending;
}
