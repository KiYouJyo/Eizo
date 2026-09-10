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

public sealed class MediaScanCoordinator
{
    private const long ProgressNotificationIntervalMilliseconds = 100;

    private readonly object _sync = new();
    private readonly Dictionary<string, MediaScanSnapshot> _snapshots =
        new(StringComparer.Ordinal);
    private readonly Dictionary<string, Task<MediaScanSnapshot>> _jobs =
        new(StringComparer.Ordinal);
    private readonly Dictionary<string, long> _lastProgressNotifications =
        new(StringComparer.Ordinal);

    private MediaScanCoordinator()
    {
    }

    public static MediaScanCoordinator Default { get; } = new();

    public event EventHandler? Changed;

    public MediaScanSnapshot? SnapshotForSource(string sourceId)
    {
        lock (_sync)
        {
            return _snapshots.TryGetValue(sourceId, out var snapshot)
                ? snapshot
                : null;
        }
    }

    public bool IsScanning(string sourceId)
    {
        lock (_sync)
            return _jobs.ContainsKey(sourceId);
    }

    public Task<MediaScanSnapshot> StartAsync(
        MediaSourceDefinition source,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(source);

        Task<MediaScanSnapshot> task;
        lock (_sync)
        {
            if (_jobs.TryGetValue(source.Id, out var existing))
                return existing;

            var started = new MediaScanSnapshot(
                source.Id,
                MediaScanStatus.Running,
                DirectoriesProcessed: 0,
                DirectoriesPending: Math.Max(
                    1,
                    source.SelectedPaths?.Count ?? 0),
                VideosDiscovered: 0,
                CurrentPath: null,
                ErrorCode: null,
                ErrorDetail: null,
                StartedUtc: DateTimeOffset.UtcNow);

            _snapshots[source.Id] = started;
            _lastProgressNotifications[source.Id] = 0;
            task = Task.Run(
                () => RunAsync(source, started, cancellationToken),
                CancellationToken.None);
            _jobs[source.Id] = task;
        }

        RaiseChanged();
        return task;
    }

    private async Task<MediaScanSnapshot> RunAsync(
        MediaSourceDefinition source,
        MediaScanSnapshot started,
        CancellationToken cancellationToken)
    {
        MediaScanSnapshot finished;

        try
        {
            var count = await MediaCatalogStore.Default.ScanSourceAsync(
                source,
                progress => UpdateProgress(started, progress),
                cancellationToken);

            var latest = SnapshotForSource(source.Id) ?? started;
            finished = latest with
            {
                Status = MediaScanStatus.Completed,
                DirectoriesPending = 0,
                VideosDiscovered = count,
                CurrentPath = null,
                ErrorCode = null,
                ErrorDetail = null,
                FinishedUtc = DateTimeOffset.UtcNow
            };
        }
        catch (OperationCanceledException)
        {
            var latest = SnapshotForSource(source.Id) ?? started;
            finished = latest with
            {
                Status = MediaScanStatus.Canceled,
                DirectoriesPending = 0,
                CurrentPath = null,
                ErrorCode = "Canceled",
                ErrorDetail = null,
                FinishedUtc = DateTimeOffset.UtcNow
            };
        }
        catch (MediaSourceException exception)
        {
            var latest = SnapshotForSource(source.Id) ?? started;
            finished = latest with
            {
                Status = MediaScanStatus.Failed,
                DirectoriesPending = 0,
                CurrentPath = null,
                ErrorCode = exception.ErrorCode,
                ErrorDetail = exception.Message,
                FinishedUtc = DateTimeOffset.UtcNow
            };
        }
        catch (Exception exception)
        {
            var latest = SnapshotForSource(source.Id) ?? started;
            finished = latest with
            {
                Status = MediaScanStatus.Failed,
                DirectoriesPending = 0,
                CurrentPath = null,
                ErrorCode = "ScanError",
                ErrorDetail = exception.Message,
                FinishedUtc = DateTimeOffset.UtcNow
            };
        }

        lock (_sync)
        {
            _snapshots[source.Id] = finished;
            _jobs.Remove(source.Id);
            _lastProgressNotifications.Remove(source.Id);
        }

        RaiseChanged();
        return finished;
    }

    private void UpdateProgress(
        MediaScanSnapshot started,
        MediaScanProgress progress)
    {
        var shouldNotify = false;
        lock (_sync)
        {
            if (!_jobs.ContainsKey(progress.SourceId))
                return;

            _snapshots[progress.SourceId] = started with
            {
                Status = MediaScanStatus.Running,
                DirectoriesProcessed = progress.DirectoriesProcessed,
                DirectoriesPending = progress.DirectoriesPending,
                VideosDiscovered = progress.VideosDiscovered,
                CurrentPath = progress.CurrentPath,
                ErrorCode = null,
                ErrorDetail = null
            };

            var now = Environment.TickCount64;
            var last = _lastProgressNotifications.GetValueOrDefault(
                progress.SourceId);
            if (now - last >= ProgressNotificationIntervalMilliseconds)
            {
                _lastProgressNotifications[progress.SourceId] = now;
                shouldNotify = true;
            }
        }

        if (shouldNotify)
            RaiseChanged();
    }

    private void RaiseChanged() =>
        Changed?.Invoke(this, EventArgs.Empty);
}
