namespace Eizo.Models;

internal sealed record WebDavCacheDiagnosticsSnapshot(
    long MemoryHits,
    long DiskHits,
    long RangeDownloads,
    long DownloadedBytes)
{
    public long CacheHits =>
        MemoryHits +
        DiskHits;

    public long TotalBlockResolutions =>
        CacheHits +
        RangeDownloads;

    public double HitRate =>
        TotalBlockResolutions == 0
            ? 0d
            : CacheHits /
              (double)TotalBlockResolutions;
}

internal static class WebDavCacheDiagnostics
{
    private static long _memoryHits;
    private static long _diskHits;
    private static long _rangeDownloads;
    private static long _downloadedBytes;

    internal static void RecordMemoryHit() =>
        Interlocked.Increment(
            ref _memoryHits);

    internal static void RecordDiskHit() =>
        Interlocked.Increment(
            ref _diskHits);

    internal static void RecordRangeDownload(
        long bytes)
    {
        Interlocked.Increment(
            ref _rangeDownloads);
        Interlocked.Add(
            ref _downloadedBytes,
            Math.Max(0, bytes));
    }

    internal static WebDavCacheDiagnosticsSnapshot Snapshot() =>
        new(
            Interlocked.Read(
                ref _memoryHits),
            Interlocked.Read(
                ref _diskHits),
            Interlocked.Read(
                ref _rangeDownloads),
            Interlocked.Read(
                ref _downloadedBytes));
}
