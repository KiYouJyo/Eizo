namespace Eizo.Models;

public enum MediaScanStatus
{
    Running = 0,
    Completed = 1,
    Failed = 2,
    Canceled = 3
}

public enum MediaScanStage
{
    Discovering = 0,
    Metadata = 1,
    Committing = 2
}

public sealed record MediaScanProgress(
    string SourceId,
    int DirectoriesProcessed,
    int DirectoriesPending,
    int VideosDiscovered,
    string? CurrentPath = null,
    MediaScanStage Stage = MediaScanStage.Discovering,
    int MetadataProcessed = 0,
    int MetadataTotal = 0,
    int MetadataResolved = 0,
    int MetadataUnresolved = 0,
    int MetadataErrors = 0);

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
    DateTimeOffset? FinishedUtc = null,
    MediaScanStage Stage = MediaScanStage.Discovering,
    int MetadataProcessed = 0,
    int MetadataTotal = 0,
    int MetadataResolved = 0,
    int MetadataUnresolved = 0,
    int MetadataErrors = 0)
{
    public int KnownDirectoryTotal =>
        DirectoriesProcessed + DirectoriesPending;
}
