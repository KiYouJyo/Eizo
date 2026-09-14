namespace Eizo.Cache;

public enum CacheCategory
{
    Media = 0,
    Artwork = 1,
    Metadata = 2,
    Subtitles = 3,
    Other = 4
}

public sealed record CacheWriteOptions(
    string? DisplayName = null,
    string? Source = null,
    string? Extension = null,
    bool? Pinned = null,
    string? GroupKey = null);

public sealed record CacheEntrySnapshot(
    string Id,
    CacheCategory Category,
    string DisplayName,
    string Source,
    string Path,
    long SizeBytes,
    DateTimeOffset CreatedAtUtc,
    DateTimeOffset LastAccessedUtc,
    bool Pinned,
    string? GroupKey = null);

public sealed record CacheSnapshot(
    long TotalBytes,
    IReadOnlyDictionary<CacheCategory, long> CategoryBytes,
    IReadOnlyList<CacheEntrySnapshot> Entries);

public sealed record CachePolicy(
    long LimitBytes,
    bool AutomaticCleanup,
    bool PreservePinned,
    long RemotePrecacheBytes);

public sealed record CacheCleanupResult(
    long BytesBefore,
    long BytesAfter,
    int EntriesRemoved);

public static class CacheDefaults
{
    public const long LimitBytes = 32L * 1024 * 1024 * 1024;
    public const long RemotePrecacheBytes = 256L * 1024 * 1024;
}

public static class CachePaths
{
    public static string DefaultRoot { get; } = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
        "Eizo",
        "Cache");
}
