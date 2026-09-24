namespace Eizo.Models;

internal enum VideoCacheDownloadStatus
{
    Downloading = 0,
    Paused = 1,
    Completed = 2,
    Failed = 3,
    Canceled = 4
}

internal sealed record VideoCacheDownloadSnapshot(
    string TaskKey,
    string Title,
    string Source,
    string SourceId,
    string Locator,
    string? GroupKey,
    string? LocalPath,
    VideoCacheDownloadStatus Status,
    long CompletedBytes,
    long TotalBytes,
    long CompletedBlocks,
    long TotalBlocks,
    double BytesPerSecond,
    string? Error,
    DateTimeOffset UpdatedUtc)
{
    public double ProgressPercent =>
        TotalBytes <= 0
            ? 0d
            : Math.Clamp(
                CompletedBytes /
                (double)TotalBytes *
                100d,
                0d,
                100d);
}

internal sealed class VideoCacheDownloadManager
{
    private readonly object _sync = new();
    private readonly Dictionary<string, DownloadEntry> _entries =
        new(StringComparer.Ordinal);
    private long _nextOrder;

    public static VideoCacheDownloadManager Default { get; } = new();

    public event EventHandler? Changed;

    public IReadOnlyList<VideoCacheDownloadSnapshot> Snapshot()
    {
        lock (_sync)
        {
            return _entries.Values
                .OrderBy(static entry =>
                    entry.Order)
                .Select(static entry =>
                    entry.Snapshot)
                .ToArray();
        }
    }

    public Task StartAsync(
        CatalogMediaItemModel item,
        string? displayMeta = null)
    {
        ArgumentNullException.ThrowIfNull(item);

        if (!WebDavVideoCacheService.Default.CanCache(item) ||
            item.Location is not { } location)
        {
            throw new InvalidOperationException(
                "Only WebDAV media can be downloaded to the video cache.");
        }

        var key = CreateTaskKey(
            location.SourceId,
            location.Locator);

        DownloadEntry entry;

        lock (_sync)
        {
            if (_entries.TryGetValue(
                    key,
                    out var existing))
            {
                if (existing.Snapshot.Status is
                    VideoCacheDownloadStatus.Downloading or
                    VideoCacheDownloadStatus.Paused)
                {
                    return existing.Completion;
                }

                if (existing.Snapshot.Status ==
                    VideoCacheDownloadStatus.Completed)
                {
                    return Task.CompletedTask;
                }

                existing.Cancellation.Dispose();
                _entries.Remove(key);
            }

            var source =
                MediaSourceStore.Default.Find(
                    location.SourceId);

            entry = new DownloadEntry(
                new VideoCacheDownloadSnapshot(
                    key,
                    item.DisplayTitle,
                    string.IsNullOrWhiteSpace(displayMeta)
                        ? source?.DisplayName ??
                          item.SourceTitle
                        : displayMeta,
                    location.SourceId,
                    location.Locator,
                    GroupKey: null,
                    LocalPath: null,
                    VideoCacheDownloadStatus.Downloading,
                    CompletedBytes: 0,
                    TotalBytes:
                        location.SizeBytes ??
                        0,
                    CompletedBlocks: 0,
                    TotalBlocks: 0,
                    BytesPerSecond: 0,
                    Error: null,
                    UpdatedUtc:
                        DateTimeOffset.UtcNow),
                ++_nextOrder);

            _entries[key] = entry;
            entry.Completion =
                RunAsync(
                    entry,
                    item);
        }

        RaiseChanged();
        return entry.Completion;
    }

    public bool TogglePause(
        string taskKey)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(taskKey);

        TaskCompletionSource<bool>? resumeSignal = null;
        var changed = false;

        lock (_sync)
        {
            if (!_entries.TryGetValue(
                    taskKey,
                    out var entry))
            {
                return false;
            }

            if (entry.Snapshot.Status ==
                VideoCacheDownloadStatus.Downloading)
            {
                entry.ResumeSignal =
                    new TaskCompletionSource<bool>(
                        TaskCreationOptions.RunContinuationsAsynchronously);
                entry.Snapshot =
                    entry.Snapshot with
                    {
                        Status =
                            VideoCacheDownloadStatus.Paused,
                        BytesPerSecond = 0,
                        UpdatedUtc =
                            DateTimeOffset.UtcNow
                    };
                ResetSpeedSample(entry);
                changed = true;
            }
            else if (entry.Snapshot.Status ==
                     VideoCacheDownloadStatus.Paused)
            {
                resumeSignal =
                    entry.ResumeSignal;
                entry.ResumeSignal = null;
                entry.Snapshot =
                    entry.Snapshot with
                    {
                        Status =
                            VideoCacheDownloadStatus.Downloading,
                        BytesPerSecond = 0,
                        UpdatedUtc =
                            DateTimeOffset.UtcNow
                    };
                ResetSpeedSample(entry);
                changed = true;
            }
        }

        resumeSignal?.TrySetResult(true);

        if (changed)
            RaiseChanged();

        return changed;
    }

    public async Task DeleteTaskAsync(
        string taskKey)
    {
        DownloadEntry? entry;

        lock (_sync)
        {
            _entries.TryGetValue(
                taskKey,
                out entry);
        }

        if (entry is null)
            return;

        entry.Cancellation.Cancel();
        ReleasePause(entry);

        try
        {
            await entry.Completion;
        }
        catch
        {
            // Deletion owns cancellation/failure cleanup.
        }

        var groupKey =
            entry.Snapshot.GroupKey;

        if (!string.IsNullOrWhiteSpace(
                groupKey))
        {
            await global::Eizo.CacheRuntime.Store.ClearGroupAsync(
                groupKey,
                preservePinned: false);
        }

        lock (_sync)
        {
            if (_entries.Remove(
                    taskKey,
                    out var removed))
            {
                removed.Cancellation.Dispose();
            }
        }

        RaiseChanged();
    }

    public async Task DeleteGroupAsync(
        string groupKey)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(groupKey);

        DownloadEntry[] matching;

        lock (_sync)
        {
            matching = _entries.Values
                .Where(entry =>
                    string.Equals(
                        entry.Snapshot.GroupKey,
                        groupKey,
                        StringComparison.Ordinal))
                .ToArray();

            foreach (var entry in matching)
            {
                entry.Cancellation.Cancel();
                ReleasePause(entry);
            }
        }

        foreach (var entry in matching)
        {
            try
            {
                await entry.Completion;
            }
            catch
            {
                // The group is being removed regardless of task outcome.
            }
        }

        await global::Eizo.CacheRuntime.Store.ClearGroupAsync(
            groupKey,
            preservePinned: false);

        lock (_sync)
        {
            foreach (var entry in matching)
            {
                _entries.Remove(
                    entry.Snapshot.TaskKey);
                entry.Cancellation.Dispose();
            }
        }

        RaiseChanged();
    }

    public void ForgetGroup(
        string groupKey)
    {
        DownloadEntry[] removed;

        lock (_sync)
        {
            removed = _entries.Values
                .Where(entry =>
                    string.Equals(
                        entry.Snapshot.GroupKey,
                        groupKey,
                        StringComparison.Ordinal))
                .ToArray();

            foreach (var entry in removed)
                _entries.Remove(entry.Snapshot.TaskKey);
        }

        foreach (var entry in removed)
            entry.Cancellation.Dispose();

        if (removed.Length > 0)
            RaiseChanged();
    }

    private async Task RunAsync(
        DownloadEntry entry,
        CatalogMediaItemModel item)
    {
        try
        {
            var progress =
                new InlineProgress<WebDavVideoCacheProgress>(
                    value =>
                        UpdateProgress(
                            entry,
                            value));

            var result =
                await WebDavVideoCacheService.Default.CacheAsync(
                    item,
                    progress,
                    entry.Cancellation.Token,
                    cancellationToken =>
                        WaitForResumeAsync(
                            entry,
                            cancellationToken),
                    entry.Snapshot.Source);

            Update(
                entry,
                snapshot => snapshot with
                {
                    GroupKey = result.GroupKey,
                    LocalPath = result.Path,
                    Status =
                        VideoCacheDownloadStatus.Completed,
                    CompletedBytes =
                        result.SizeBytes,
                    TotalBytes =
                        result.SizeBytes,
                    CompletedBlocks =
                        result.BlockCount,
                    TotalBlocks =
                        result.BlockCount,
                    BytesPerSecond = 0,
                    Error = null,
                    UpdatedUtc =
                        DateTimeOffset.UtcNow
                });
        }
        catch (OperationCanceledException)
        {
            Update(
                entry,
                snapshot => snapshot with
                {
                    Status =
                        VideoCacheDownloadStatus.Canceled,
                    BytesPerSecond = 0,
                    UpdatedUtc =
                        DateTimeOffset.UtcNow
                });
            throw;
        }
        catch (Exception exception)
        {
            Update(
                entry,
                snapshot => snapshot with
                {
                    Status =
                        VideoCacheDownloadStatus.Failed,
                    BytesPerSecond = 0,
                    Error =
                        exception.Message,
                    UpdatedUtc =
                        DateTimeOffset.UtcNow
                });
            throw;
        }
    }

    private async Task WaitForResumeAsync(
        DownloadEntry entry,
        CancellationToken cancellationToken)
    {
        while (true)
        {
            Task? waitTask;

            lock (_sync)
            {
                if (entry.Snapshot.Status !=
                    VideoCacheDownloadStatus.Paused)
                {
                    return;
                }

                waitTask =
                    entry.ResumeSignal?.Task;
            }

            if (waitTask is null)
                return;

            await waitTask.WaitAsync(
                cancellationToken);
        }
    }

    private void UpdateProgress(
        DownloadEntry entry,
        WebDavVideoCacheProgress progress)
    {
        var now =
            DateTimeOffset.UtcNow;

        lock (_sync)
        {
            var elapsed =
                (now -
                 entry.LastSpeedSampleUtc)
                .TotalSeconds;
            var downloadedDelta =
                progress.NetworkDownloadedBytes -
                entry.LastNetworkDownloadedBytes;

            if (downloadedDelta > 0 &&
                elapsed > 0)
            {
                var instant =
                    downloadedDelta /
                    elapsed;

                entry.SmoothedBytesPerSecond =
                    entry.SmoothedBytesPerSecond <= 0
                        ? instant
                        : entry.SmoothedBytesPerSecond *
                          0.65d +
                          instant *
                          0.35d;
            }
            else if (elapsed >= 1d)
            {
                entry.SmoothedBytesPerSecond = 0;
            }

            entry.LastNetworkDownloadedBytes =
                progress.NetworkDownloadedBytes;
            entry.LastSpeedSampleUtc = now;

            var snapshot =
                entry.Snapshot;

            entry.Snapshot =
                snapshot with
                {
                    GroupKey =
                        progress.GroupKey,
                    Status =
                        snapshot.Status ==
                        VideoCacheDownloadStatus.Paused
                            ? VideoCacheDownloadStatus.Paused
                            : VideoCacheDownloadStatus.Downloading,
                    CompletedBytes =
                        progress.CompletedBytes,
                    TotalBytes =
                        progress.TotalBytes,
                    CompletedBlocks =
                        progress.CompletedBlocks,
                    TotalBlocks =
                        progress.TotalBlocks,
                    BytesPerSecond =
                        snapshot.Status ==
                        VideoCacheDownloadStatus.Paused
                            ? 0
                            : entry.SmoothedBytesPerSecond,
                    Error = null,
                    UpdatedUtc = now
                };
        }

        RaiseChanged();
    }

    private static void ResetSpeedSample(
        DownloadEntry entry)
    {
        entry.LastSpeedSampleUtc =
            DateTimeOffset.UtcNow;
        entry.SmoothedBytesPerSecond = 0;
    }

    private void Update(
        DownloadEntry entry,
        Func<VideoCacheDownloadSnapshot, VideoCacheDownloadSnapshot> update)
    {
        lock (_sync)
        {
            entry.Snapshot =
                update(entry.Snapshot);
        }

        RaiseChanged();
    }

    private void ReleasePause(
        DownloadEntry entry)
    {
        var signal =
            entry.ResumeSignal;
        entry.ResumeSignal = null;
        signal?.TrySetResult(true);
    }

    private void RaiseChanged() =>
        Changed?.Invoke(
            this,
            EventArgs.Empty);

    private static string CreateTaskKey(
        string sourceId,
        string locator) =>
        sourceId +
        "\n" +
        locator;

    private sealed class DownloadEntry
    {
        public DownloadEntry(
            VideoCacheDownloadSnapshot snapshot,
            long order)
        {
            Snapshot = snapshot;
            Order = order;
        }

        public VideoCacheDownloadSnapshot Snapshot { get; set; }

        public long Order { get; }

        public CancellationTokenSource Cancellation { get; } = new();

        public TaskCompletionSource<bool>? ResumeSignal { get; set; }

        public long LastNetworkDownloadedBytes { get; set; }

        public DateTimeOffset LastSpeedSampleUtc { get; set; } =
            DateTimeOffset.UtcNow;

        public double SmoothedBytesPerSecond { get; set; }

        public Task Completion { get; set; } =
            Task.CompletedTask;
    }

    private sealed class InlineProgress<T>(
        Action<T> callback)
        : IProgress<T>
    {
        public void Report(T value) =>
            callback(value);
    }
}
