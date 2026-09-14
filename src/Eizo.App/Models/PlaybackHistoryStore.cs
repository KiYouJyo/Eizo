using System.Text.Json;

namespace Eizo.Models;

internal sealed class PlaybackHistoryStore
{
    private static readonly JsonSerializerOptions SerializerOptions =
        new() { WriteIndented = true };

    private static readonly string StorePath = Path.Combine(
        Environment.GetFolderPath(
            Environment.SpecialFolder.LocalApplicationData),
        "Eizo",
        "playback-history.json");

    private readonly object _sync = new();
    private readonly Dictionary<string, PlaybackHistoryEntry> _entries;
    private DateTimeOffset _lastPersistedAtUtc = DateTimeOffset.MinValue;

    private PlaybackHistoryStore()
    {
        _entries = LoadCore()
            .ToDictionary(
                static entry => entry.ItemKey,
                StringComparer.Ordinal);
    }

    internal static PlaybackHistoryStore Default { get; } = new();

    internal event EventHandler? Changed;

    internal IReadOnlyList<PlaybackHistoryEntry> Snapshot() 
    {
        lock (_sync)
        {
            return _entries.Values
                .OrderByDescending(static entry => entry.LastWatchedUtc)
                .ToArray();
        }
    }

    internal TimeSpan GetResumePosition(
        CatalogMediaItemModel? item)
    {
        if (item is null)
            return TimeSpan.Zero;

        var key = MediaCatalogStore.ItemKey(item);

        lock (_sync)
        {
            if (!_entries.TryGetValue(key, out var entry))
                return TimeSpan.Zero;

            if (entry.DurationSeconds > 0 &&
                entry.PositionSeconds / entry.DurationSeconds >= 0.95d)
            {
                return TimeSpan.Zero;
            }

            return TimeSpan.FromSeconds(
                Math.Max(0d, entry.PositionSeconds));
        }
    }

    internal void Touch(CatalogMediaItemModel? item)
    {
        if (item is null)
            return;

        var key = MediaCatalogStore.ItemKey(item);
        var now = DateTimeOffset.UtcNow;

        lock (_sync)
        {
            if (_entries.TryGetValue(key, out var existing))
            {
                _entries[key] = existing with
                {
                    LastWatchedUtc = now,
                };
            }
            else
            {
                _entries[key] = new PlaybackHistoryEntry(
                    key,
                    PositionSeconds: 0d,
                    DurationSeconds: 0d,
                    LastWatchedUtc: now);
            }

            SaveCoreLocked(now);
        }

        Changed?.Invoke(this, EventArgs.Empty);
    }

    internal void Record(
        CatalogMediaItemModel? item,
        TimeSpan position,
        TimeSpan duration)
    {
        if (item is null)
            return;

        var key = MediaCatalogStore.ItemKey(item);
        var now = DateTimeOffset.UtcNow;
        var positionSeconds = Math.Max(
            0d,
            position.TotalSeconds);
        var durationSeconds = Math.Max(
            0d,
            duration.TotalSeconds);

        var persisted = false;

        lock (_sync)
        {
            _entries[key] = new PlaybackHistoryEntry(
                key,
                positionSeconds,
                durationSeconds,
                now);

            if (now - _lastPersistedAtUtc >= TimeSpan.FromSeconds(5))
            {
                SaveCoreLocked(now);
                persisted = true;
            }
        }

        if (persisted)
            Changed?.Invoke(this, EventArgs.Empty);
    }

    internal void Flush()
    {
        lock (_sync)
            SaveCoreLocked(DateTimeOffset.UtcNow);
    }

    private void SaveCoreLocked(DateTimeOffset now)
    {
        try
        {
            Directory.CreateDirectory(
                Path.GetDirectoryName(StorePath)!);

            var temporaryPath =
                $"{StorePath}.{Environment.ProcessId}.{Guid.NewGuid():N}.tmp";

            var document = new PlaybackHistoryDocument(
                SchemaVersion: 1,
                Entries: _entries.Values
                    .OrderByDescending(static entry => entry.LastWatchedUtc)
                    .Take(200)
                    .ToList());

            File.WriteAllText(
                temporaryPath,
                JsonSerializer.Serialize(
                    document,
                    SerializerOptions));

            File.Move(
                temporaryPath,
                StorePath,
                overwrite: true);

            _lastPersistedAtUtc = now;
        }
        catch (IOException)
        {
        }
        catch (UnauthorizedAccessException)
        {
        }
    }

    private static IReadOnlyList<PlaybackHistoryEntry> LoadCore()
    {
        try
        {
            if (!File.Exists(StorePath))
                return [];

            var document =
                JsonSerializer.Deserialize<PlaybackHistoryDocument>(
                    File.ReadAllText(StorePath),
                    SerializerOptions);

            return document is { SchemaVersion: 1 }
                ? document.Entries ?? []
                : [];
        }
        catch (IOException)
        {
            return [];
        }
        catch (UnauthorizedAccessException)
        {
            return [];
        }
        catch (JsonException)
        {
            return [];
        }
    }

    private sealed record PlaybackHistoryDocument(
        int SchemaVersion,
        List<PlaybackHistoryEntry> Entries);
}

internal sealed record PlaybackHistoryEntry(
    string ItemKey,
    double PositionSeconds,
    double DurationSeconds,
    DateTimeOffset LastWatchedUtc)
{
    internal double ProgressPercent =>
        DurationSeconds <= 0d
            ? 0d
            : Math.Clamp(
                PositionSeconds / DurationSeconds * 100d,
                0d,
                100d);
}
