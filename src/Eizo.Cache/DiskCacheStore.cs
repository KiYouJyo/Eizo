using System.Collections.Concurrent;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;

namespace Eizo.Cache;

public sealed class DiskCacheStore
{
    private const int IndexSchemaVersion = 1;
    private const double CleanupTargetRatio = 0.90d;

    private static readonly JsonSerializerOptions SerializerOptions =
        new()
        {
            WriteIndented = true
        };

    private static readonly ConcurrentDictionary<string, SemaphoreSlim> RootGates =
        new(StringComparer.OrdinalIgnoreCase);

    private readonly SemaphoreSlim _gate;
    private readonly string _rootPath;
    private readonly string _indexPath;

    public DiskCacheStore(string? rootPath = null)
    {
        _rootPath = Path.GetFullPath(
            rootPath ?? CachePaths.DefaultRoot);
        _indexPath = Path.Combine(
            _rootPath,
            "index.json");
        _gate = RootGates.GetOrAdd(
            _rootPath,
            static _ => new SemaphoreSlim(1, 1));
    }

    public string RootPath => _rootPath;

    public async Task<string?> ReadTextAsync(
        CacheCategory category,
        string key,
        CancellationToken cancellationToken = default)
    {
        var bytes = await ReadBytesAsync(
            category,
            key,
            cancellationToken);
        return bytes is null
            ? null
            : Encoding.UTF8.GetString(bytes);
    }

    public async Task<byte[]?> ReadBytesAsync(
        CacheCategory category,
        string key,
        CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(key);

        await _gate.WaitAsync(cancellationToken);
        try
        {
            var index = LoadIndexCore();
            var id = CreateId(category, key);

            if (!index.Entries.TryGetValue(id, out var entry))
                return null;

            var path = ResolveEntryPath(entry.RelativePath);
            if (!File.Exists(path))
            {
                index.Entries.Remove(id);
                await SaveIndexCoreAsync(
                    index,
                    cancellationToken);
                return null;
            }

            var bytes = await File.ReadAllBytesAsync(
                path,
                cancellationToken);

            index.Entries[id] = entry with
            {
                SizeBytes = bytes.LongLength,
                LastAccessedUtc = DateTimeOffset.UtcNow
            };
            await SaveIndexCoreAsync(
                index,
                cancellationToken);

            return bytes;
        }
        finally
        {
            _gate.Release();
        }
    }

    public async Task<string?> TryGetPathAsync(
        CacheCategory category,
        string key,
        CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(key);

        await _gate.WaitAsync(cancellationToken);
        try
        {
            var index = LoadIndexCore();
            var id = CreateId(category, key);

            if (!index.Entries.TryGetValue(id, out var entry))
                return null;

            var path = ResolveEntryPath(entry.RelativePath);
            if (!File.Exists(path))
            {
                index.Entries.Remove(id);
                await SaveIndexCoreAsync(
                    index,
                    cancellationToken);
                return null;
            }

            var file = new FileInfo(path);
            index.Entries[id] = entry with
            {
                SizeBytes = file.Length,
                LastAccessedUtc = DateTimeOffset.UtcNow
            };
            await SaveIndexCoreAsync(
                index,
                cancellationToken);

            return path;
        }
        finally
        {
            _gate.Release();
        }
    }

    public Task<CacheEntrySnapshot> WriteTextAsync(
        CacheCategory category,
        string key,
        string value,
        CacheWriteOptions? options = null,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(value);
        return WriteBytesAsync(
            category,
            key,
            Encoding.UTF8.GetBytes(value),
            options,
            cancellationToken);
    }

    public async Task<CacheEntrySnapshot> WriteBytesAsync(
        CacheCategory category,
        string key,
        byte[] value,
        CacheWriteOptions? options = null,
        CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(key);
        ArgumentNullException.ThrowIfNull(value);

        options ??= new CacheWriteOptions();

        await _gate.WaitAsync(cancellationToken);
        try
        {
            Directory.CreateDirectory(_rootPath);

            var index = LoadIndexCore();
            var id = CreateId(category, key);
            index.Entries.TryGetValue(id, out var existing);

            var relativePath = existing?.RelativePath;
            if (string.IsNullOrWhiteSpace(relativePath))
            {
                var extension = NormalizeExtension(
                    options.Extension);
                relativePath = Path.Combine(
                        CategoryFolder(category),
                        id + extension)
                    .Replace(
                        Path.DirectorySeparatorChar,
                        '/');
            }

            var path = ResolveEntryPath(relativePath);
            Directory.CreateDirectory(
                Path.GetDirectoryName(path)!);

            var temporaryPath =
                path +
                "." +
                Environment.ProcessId +
                "." +
                Guid.NewGuid().ToString("N") +
                ".tmp";

            await File.WriteAllBytesAsync(
                temporaryPath,
                value,
                cancellationToken);
            File.Move(
                temporaryPath,
                path,
                overwrite: true);

            var now = DateTimeOffset.UtcNow;
            var entry = new CacheIndexEntry(
                id,
                category,
                relativePath,
                string.IsNullOrWhiteSpace(options.DisplayName)
                    ? key
                    : options.DisplayName!,
                string.IsNullOrWhiteSpace(options.Source)
                    ? category.ToString()
                    : options.Source!,
                value.LongLength,
                existing?.CreatedAtUtc ?? now,
                now,
                options.Pinned ??
                existing?.Pinned ??
                false)
            {
                GroupKey =
                    string.IsNullOrWhiteSpace(options.GroupKey)
                        ? existing?.GroupKey
                        : options.GroupKey
            };

            index.Entries[id] = entry;
            await SaveIndexCoreAsync(
                index,
                cancellationToken);

            return ToSnapshot(
                entry,
                path);
        }
        finally
        {
            _gate.Release();
        }
    }

    public async Task<CacheSnapshot> GetSnapshotAsync(
        CancellationToken cancellationToken = default)
    {
        await _gate.WaitAsync(cancellationToken);
        try
        {
            var index = LoadIndexCore();
            var entries = new List<CacheEntrySnapshot>();
            var changed = false;

            foreach (var pair in index.Entries.ToArray())
            {
                cancellationToken.ThrowIfCancellationRequested();

                var entry = pair.Value;
                var path = ResolveEntryPath(
                    entry.RelativePath);

                if (!File.Exists(path))
                {
                    index.Entries.Remove(pair.Key);
                    changed = true;
                    continue;
                }

                var file = new FileInfo(path);
                if (file.Length != entry.SizeBytes)
                {
                    entry = entry with
                    {
                        SizeBytes = file.Length
                    };
                    index.Entries[pair.Key] = entry;
                    changed = true;
                }

                entries.Add(
                    ToSnapshot(
                        entry,
                        path));
            }

            changed |= DeleteOrphanFilesCore(index);

            if (changed)
            {
                await SaveIndexCoreAsync(
                    index,
                    cancellationToken);
            }

            return CreateSnapshot(entries);
        }
        finally
        {
            _gate.Release();
        }
    }

    public async Task<bool> DeleteAsync(
        string id,
        CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(id);

        await _gate.WaitAsync(cancellationToken);
        try
        {
            var index = LoadIndexCore();
            if (!index.Entries.Remove(
                    id,
                    out var entry))
            {
                return false;
            }

            DeleteEntryFileCore(entry);
            await SaveIndexCoreAsync(
                index,
                cancellationToken);
            return true;
        }
        finally
        {
            _gate.Release();
        }
    }

    public async Task<int> ClearAsync(
        CacheCategory? category = null,
        bool preservePinned = false,
        CancellationToken cancellationToken = default)
    {
        await _gate.WaitAsync(cancellationToken);
        try
        {
            var index = LoadIndexCore();
            var removed = 0;

            foreach (var pair in index.Entries.ToArray())
            {
                cancellationToken.ThrowIfCancellationRequested();

                var entry = pair.Value;
                if (category is not null &&
                    entry.Category != category.Value)
                {
                    continue;
                }

                if (preservePinned &&
                    entry.Pinned)
                {
                    continue;
                }

                DeleteEntryFileCore(entry);
                index.Entries.Remove(pair.Key);
                removed++;
            }

            if (removed > 0)
            {
                await SaveIndexCoreAsync(
                    index,
                    cancellationToken);
            }

            return removed;
        }
        finally
        {
            _gate.Release();
        }
    }

    public async Task<int> ClearGroupAsync(
        string groupKey,
        bool preservePinned = false,
        CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(groupKey);

        await _gate.WaitAsync(cancellationToken);
        try
        {
            var index = LoadIndexCore();
            var removed = 0;

            foreach (var pair in index.Entries.ToArray())
            {
                cancellationToken.ThrowIfCancellationRequested();

                var entry = pair.Value;
                if (!string.Equals(
                        entry.GroupKey,
                        groupKey,
                        StringComparison.Ordinal))
                {
                    continue;
                }

                if (preservePinned &&
                    entry.Pinned)
                {
                    continue;
                }

                DeleteEntryFileCore(entry);
                index.Entries.Remove(pair.Key);
                removed++;
            }

            if (removed > 0)
            {
                await SaveIndexCoreAsync(
                    index,
                    cancellationToken);
            }

            return removed;
        }
        finally
        {
            _gate.Release();
        }
    }

    public async Task<CacheCleanupResult> TrimGroupAsync(
        string groupKey,
        long limitBytes,
        bool preservePinned = false,
        CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(groupKey);

        await _gate.WaitAsync(cancellationToken);
        try
        {
            var index = LoadIndexCore();
            var groupEntries = new List<CacheIndexEntry>();
            long bytesBefore = 0;
            var changed = false;

            foreach (var pair in index.Entries.ToArray())
            {
                cancellationToken.ThrowIfCancellationRequested();

                var entry = pair.Value;
                if (!string.Equals(
                        entry.GroupKey,
                        groupKey,
                        StringComparison.Ordinal))
                {
                    continue;
                }

                var path = ResolveEntryPath(
                    entry.RelativePath);

                if (!File.Exists(path))
                {
                    index.Entries.Remove(pair.Key);
                    changed = true;
                    continue;
                }

                var size = new FileInfo(path).Length;
                if (size != entry.SizeBytes)
                {
                    entry = entry with
                    {
                        SizeBytes = size
                    };
                    index.Entries[pair.Key] = entry;
                    changed = true;
                }

                bytesBefore += size;
                if (!preservePinned ||
                    !entry.Pinned)
                {
                    groupEntries.Add(entry);
                }
            }

            var limit = Math.Max(
                0,
                limitBytes);
            var bytesAfter = bytesBefore;
            var removed = 0;

            foreach (var entry in groupEntries
                         .OrderBy(static value =>
                             value.LastAccessedUtc)
                         .ThenBy(static value =>
                             value.CreatedAtUtc))
            {
                if (bytesAfter <= limit)
                    break;

                DeleteEntryFileCore(entry);
                index.Entries.Remove(entry.Id);
                bytesAfter -= entry.SizeBytes;
                removed++;
                changed = true;
            }

            if (changed)
            {
                await SaveIndexCoreAsync(
                    index,
                    cancellationToken);
            }

            return new CacheCleanupResult(
                bytesBefore,
                Math.Max(0, bytesAfter),
                removed);
        }
        finally
        {
            _gate.Release();
        }
    }

    public async Task<CacheCleanupResult> EnforceLimitAsync(
        CachePolicy policy,
        CancellationToken cancellationToken = default)
    {
        if (!policy.AutomaticCleanup)
        {
            var snapshot = await GetSnapshotAsync(
                cancellationToken);
            return new CacheCleanupResult(
                snapshot.TotalBytes,
                snapshot.TotalBytes,
                0);
        }

        await _gate.WaitAsync(cancellationToken);
        try
        {
            var index = LoadIndexCore();
            var candidates = new List<CacheIndexEntry>();
            long bytesBefore = 0;
            var changed = false;

            foreach (var pair in index.Entries.ToArray())
            {
                cancellationToken.ThrowIfCancellationRequested();

                var entry = pair.Value;
                var path = ResolveEntryPath(
                    entry.RelativePath);

                if (!File.Exists(path))
                {
                    index.Entries.Remove(pair.Key);
                    changed = true;
                    continue;
                }

                var size = new FileInfo(path).Length;
                if (size != entry.SizeBytes)
                {
                    entry = entry with
                    {
                        SizeBytes = size
                    };
                    index.Entries[pair.Key] = entry;
                    changed = true;
                }

                bytesBefore += size;

                if (!policy.PreservePinned ||
                    !entry.Pinned)
                {
                    candidates.Add(entry);
                }
            }

            var limit = Math.Max(
                0,
                policy.LimitBytes);

            if (bytesBefore <= limit)
            {
                if (changed)
                {
                    await SaveIndexCoreAsync(
                        index,
                        cancellationToken);
                }

                return new CacheCleanupResult(
                    bytesBefore,
                    bytesBefore,
                    0);
            }

            var target = limit == 0
                ? 0
                : (long)Math.Floor(
                    limit *
                    CleanupTargetRatio);

            var bytesAfter = bytesBefore;
            var removed = 0;

            foreach (var entry in candidates
                         .OrderBy(static value =>
                             value.LastAccessedUtc)
                         .ThenBy(static value =>
                             value.CreatedAtUtc))
            {
                cancellationToken.ThrowIfCancellationRequested();

                if (bytesAfter <= target)
                    break;

                DeleteEntryFileCore(entry);
                index.Entries.Remove(entry.Id);
                bytesAfter -= entry.SizeBytes;
                removed++;
                changed = true;
            }

            if (changed)
            {
                await SaveIndexCoreAsync(
                    index,
                    cancellationToken);
            }

            return new CacheCleanupResult(
                bytesBefore,
                Math.Max(0, bytesAfter),
                removed);
        }
        finally
        {
            _gate.Release();
        }
    }

    public async Task<bool> SetPinnedAsync(
        string id,
        bool pinned,
        CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(id);

        await _gate.WaitAsync(cancellationToken);
        try
        {
            var index = LoadIndexCore();
            if (!index.Entries.TryGetValue(
                    id,
                    out var entry))
            {
                return false;
            }

            if (entry.Pinned == pinned)
                return true;

            index.Entries[id] = entry with
            {
                Pinned = pinned
            };
            await SaveIndexCoreAsync(
                index,
                cancellationToken);
            return true;
        }
        finally
        {
            _gate.Release();
        }
    }

    private CacheIndexDocument LoadIndexCore()
    {
        if (!File.Exists(_indexPath))
            return new CacheIndexDocument();

        try
        {
            var json = File.ReadAllText(
                _indexPath,
                Encoding.UTF8);
            var index =
                JsonSerializer.Deserialize<CacheIndexDocument>(
                    json,
                    SerializerOptions);

            if (index is null ||
                index.SchemaVersion != IndexSchemaVersion)
            {
                return new CacheIndexDocument();
            }

            return index;
        }
        catch (
            Exception exception)
            when (exception is IOException or
                  UnauthorizedAccessException or
                  JsonException or
                  NotSupportedException)
        {
            return new CacheIndexDocument();
        }
    }

    private async Task SaveIndexCoreAsync(
        CacheIndexDocument index,
        CancellationToken cancellationToken)
    {
        Directory.CreateDirectory(_rootPath);

        var temporaryPath =
            _indexPath +
            "." +
            Environment.ProcessId +
            "." +
            Guid.NewGuid().ToString("N") +
            ".tmp";

        var json = JsonSerializer.Serialize(
            index,
            SerializerOptions);
        await File.WriteAllTextAsync(
            temporaryPath,
            json,
            Encoding.UTF8,
            cancellationToken);
        File.Move(
            temporaryPath,
            _indexPath,
            overwrite: true);
    }

    private bool DeleteOrphanFilesCore(
        CacheIndexDocument index)
    {
        if (!Directory.Exists(_rootPath))
            return false;

        var known = index.Entries.Values
            .Select(entry =>
                ResolveEntryPath(
                    entry.RelativePath))
            .ToHashSet(
                StringComparer.OrdinalIgnoreCase);

        var deleted = false;

        foreach (var category in Enum.GetValues<CacheCategory>())
        {
            var directory = Path.Combine(
                _rootPath,
                CategoryFolder(category));
            if (!Directory.Exists(directory))
                continue;

            foreach (var file in Directory.EnumerateFiles(
                         directory,
                         "*",
                         SearchOption.AllDirectories))
            {
                if (known.Contains(
                        Path.GetFullPath(file)))
                {
                    continue;
                }

                try
                {
                    File.Delete(file);
                    deleted = true;
                }
                catch (
                    Exception exception)
                    when (exception is IOException or
                          UnauthorizedAccessException)
                {
                }
            }
        }

        return deleted;
    }

    private void DeleteEntryFileCore(
        CacheIndexEntry entry)
    {
        try
        {
            var path = ResolveEntryPath(
                entry.RelativePath);
            if (File.Exists(path))
                File.Delete(path);
        }
        catch (
            Exception exception)
            when (exception is IOException or
                  UnauthorizedAccessException)
        {
        }
    }

    private string ResolveEntryPath(
        string relativePath)
    {
        var normalized = relativePath.Replace(
            '/',
            Path.DirectorySeparatorChar);
        var path = Path.GetFullPath(
            Path.Combine(
                _rootPath,
                normalized));

        var rootPrefix =
            _rootPath.TrimEnd(
                Path.DirectorySeparatorChar,
                Path.AltDirectorySeparatorChar) +
            Path.DirectorySeparatorChar;

        if (!path.StartsWith(
                rootPrefix,
                StringComparison.OrdinalIgnoreCase))
        {
            throw new InvalidDataException(
                "Cache index path escapes the cache root.");
        }

        return path;
    }

    private static CacheSnapshot CreateSnapshot(
        IReadOnlyList<CacheEntrySnapshot> entries)
    {
        var categoryBytes =
            Enum.GetValues<CacheCategory>()
                .ToDictionary(
                    static value => value,
                    static _ => 0L);

        long total = 0;

        foreach (var entry in entries)
        {
            total += entry.SizeBytes;
            categoryBytes[entry.Category] +=
                entry.SizeBytes;
        }

        return new CacheSnapshot(
            total,
            categoryBytes,
            entries
                .OrderByDescending(
                    static entry =>
                        entry.LastAccessedUtc)
                .ThenBy(
                    static entry =>
                        entry.DisplayName,
                    StringComparer.CurrentCultureIgnoreCase)
                .ToArray());
    }

    private static CacheEntrySnapshot ToSnapshot(
        CacheIndexEntry entry,
        string path) =>
        new(
            entry.Id,
            entry.Category,
            entry.DisplayName,
            entry.Source,
            path,
            entry.SizeBytes,
            entry.CreatedAtUtc,
            entry.LastAccessedUtc,
            entry.Pinned,
            entry.GroupKey);

    private static string CreateId(
        CacheCategory category,
        string key)
    {
        var input =
            ((int)category).ToString(
                System.Globalization.CultureInfo.InvariantCulture) +
            ":" +
            key;
        var hash = SHA256.HashData(
            Encoding.UTF8.GetBytes(input));
        return Convert.ToHexString(hash)
            .ToLowerInvariant();
    }

    private static string NormalizeExtension(
        string? extension)
    {
        if (string.IsNullOrWhiteSpace(extension))
            return ".cache";

        var value = extension.Trim();
        if (!value.StartsWith(
                ".",
                StringComparison.Ordinal))
        {
            value = "." + value;
        }

        if (value.Length > 16 ||
            value.Skip(1).Any(
                static character =>
                    !char.IsLetterOrDigit(character)))
        {
            return ".cache";
        }

        return value.ToLowerInvariant();
    }

    private static string CategoryFolder(
        CacheCategory category) =>
        category switch
        {
            CacheCategory.Media => "media",
            CacheCategory.Artwork => "artwork",
            CacheCategory.Metadata => "metadata",
            CacheCategory.Subtitles => "subtitles",
            _ => "other"
        };

}

internal sealed class CacheIndexDocument
{
    public CacheIndexDocument()
    {
    }

    public int SchemaVersion { get; init; } = 1;

    public Dictionary<string, CacheIndexEntry> Entries { get; init; } =
        new(StringComparer.Ordinal);
}

internal sealed record CacheIndexEntry
{
    public CacheIndexEntry(
        string id,
        CacheCategory category,
        string relativePath,
        string displayName,
        string source,
        long sizeBytes,
        DateTimeOffset createdAtUtc,
        DateTimeOffset lastAccessedUtc,
        bool pinned)
    {
        Id = id;
        Category = category;
        RelativePath = relativePath;
        DisplayName = displayName;
        Source = source;
        SizeBytes = sizeBytes;
        CreatedAtUtc = createdAtUtc;
        LastAccessedUtc = lastAccessedUtc;
        Pinned = pinned;
    }

    public string Id { get; init; }
    public CacheCategory Category { get; init; }
    public string RelativePath { get; init; }
    public string DisplayName { get; init; }
    public string Source { get; init; }
    public long SizeBytes { get; init; }
    public DateTimeOffset CreatedAtUtc { get; init; }
    public DateTimeOffset LastAccessedUtc { get; init; }
    public bool Pinned { get; init; }
    public string? GroupKey { get; init; }
}
