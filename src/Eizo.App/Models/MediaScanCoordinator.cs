using Eizo.Localization;
using Eizo.MetadataIntegration;
using Windows.Storage;

namespace Eizo.Models;

public sealed class MediaScanCoordinator
{
    private const long ProgressNotificationIntervalMilliseconds = 150;

    private readonly object _sync = new();
    private readonly Dictionary<string, MediaScanSnapshot> _snapshots =
        new(StringComparer.Ordinal);
    private readonly Dictionary<string, Task<MediaScanSnapshot>> _jobs =
        new(StringComparer.Ordinal);
    private readonly Dictionary<string, long> _lastProgressNotifications =
        new(StringComparer.Ordinal);
    private readonly HashSet<string> _metadataJobs =
        new(StringComparer.Ordinal);
    private readonly Lazy<MediaMetadataService?> _metadataService;

    private MediaScanCoordinator()
    {
        _metadataService = new Lazy<MediaMetadataService?>(
            CreateMetadataService,
            LazyThreadSafetyMode.ExecutionAndPublication);
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

    public bool IsScraping(string sourceId)
    {
        lock (_sync)
            return _metadataJobs.Contains(sourceId);
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
                StartedUtc: DateTimeOffset.UtcNow,
                Stage: MediaScanStage.Discovering);

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

    public Task<MediaScanSnapshot> StartMetadataAsync(
        MediaSourceDefinition source,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(source);

        Task<MediaScanSnapshot> task;
        lock (_sync)
        {
            if (_jobs.TryGetValue(source.Id, out var existing))
                return existing;

            var mediaCount =
                MediaCatalogStore.Default.SnapshotForSource(source.Id).Count;
            var started = new MediaScanSnapshot(
                source.Id,
                MediaScanStatus.Running,
                DirectoriesProcessed: 0,
                DirectoriesPending: 0,
                VideosDiscovered: mediaCount,
                CurrentPath: null,
                ErrorCode: null,
                ErrorDetail: null,
                StartedUtc: DateTimeOffset.UtcNow,
                Stage: MediaScanStage.Metadata);

            _snapshots[source.Id] = started;
            _lastProgressNotifications[source.Id] = 0;
            _metadataJobs.Add(source.Id);
            task = Task.Run(
                () => RunMetadataAsync(source, started, cancellationToken),
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
                FinishedUtc = DateTimeOffset.UtcNow,
                Stage = MediaScanStage.Committing
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
            _metadataJobs.Remove(source.Id);
            _lastProgressNotifications.Remove(source.Id);
        }

        RaiseChanged();
        return finished;
    }

    private async Task<MediaScanSnapshot> RunMetadataAsync(
        MediaSourceDefinition source,
        MediaScanSnapshot started,
        CancellationToken cancellationToken)
    {
        MediaScanSnapshot finished;

        try
        {
            var processed =
                await MediaCatalogStore.Default.ScrapeSourceMetadataAsync(
                    source.Id,
                    _metadataService.Value,
                    progress => UpdateProgress(started, progress),
                    cancellationToken);

            var latest = SnapshotForSource(source.Id) ?? started;
            finished = latest with
            {
                Status = MediaScanStatus.Completed,
                DirectoriesPending = 0,
                CurrentPath = null,
                ErrorCode = null,
                ErrorDetail = null,
                FinishedUtc = DateTimeOffset.UtcNow,
                Stage = MediaScanStage.Committing,
                MetadataProcessed = Math.Max(
                    latest.MetadataProcessed,
                    processed),
                MetadataTotal = Math.Max(
                    latest.MetadataTotal,
                    processed)
            };
        }
        catch (OperationCanceledException)
        {
            var latest = SnapshotForSource(source.Id) ?? started;
            finished = latest with
            {
                Status = MediaScanStatus.Canceled,
                CurrentPath = null,
                ErrorCode = "Canceled",
                ErrorDetail = null,
                FinishedUtc = DateTimeOffset.UtcNow
            };
        }
        catch (Exception exception)
        {
            var latest = SnapshotForSource(source.Id) ?? started;
            finished = latest with
            {
                Status = MediaScanStatus.Failed,
                CurrentPath = null,
                ErrorCode = "MetadataError",
                ErrorDetail = exception.Message,
                FinishedUtc = DateTimeOffset.UtcNow
            };
        }

        lock (_sync)
        {
            _snapshots[source.Id] = finished;
            _jobs.Remove(source.Id);
            _metadataJobs.Remove(source.Id);
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

            var previousStage =
                _snapshots.TryGetValue(progress.SourceId, out var previous)
                    ? previous.Stage
                    : MediaScanStage.Discovering;

            _snapshots[progress.SourceId] = started with
            {
                Status = MediaScanStatus.Running,
                DirectoriesProcessed = progress.DirectoriesProcessed,
                DirectoriesPending = progress.DirectoriesPending,
                VideosDiscovered = progress.VideosDiscovered,
                CurrentPath = progress.CurrentPath,
                ErrorCode = null,
                ErrorDetail = null,
                Stage = progress.Stage,
                MetadataProcessed = progress.MetadataProcessed,
                MetadataTotal = progress.MetadataTotal,
                MetadataResolved = progress.MetadataResolved,
                MetadataUnresolved = progress.MetadataUnresolved,
                MetadataErrors = progress.MetadataErrors
            };

            var now = Environment.TickCount64;
            var last = _lastProgressNotifications.GetValueOrDefault(
                progress.SourceId);

            var stageChanged = progress.Stage != previousStage;

            if (stageChanged ||
                now - last >= ProgressNotificationIntervalMilliseconds ||
                (progress.Stage == MediaScanStage.Metadata &&
                 progress.MetadataTotal > 0 &&
                 progress.MetadataProcessed == progress.MetadataTotal))
            {
                _lastProgressNotifications[progress.SourceId] = now;
                shouldNotify = true;
            }
        }

        if (shouldNotify)
            RaiseChanged();
    }

    private static MediaMetadataService? CreateMetadataService()
    {
        if (string.Equals(
                Environment.GetEnvironmentVariable("EIZO_METADATA_DISABLE"),
                "1",
                StringComparison.Ordinal))
        {
            return null;
        }

        var cacheDirectory = Path.Combine(
            ApplicationData.Current.LocalCacheFolder.Path,
            "Eizo",
            "MetadataCache");

        var options = new MediaMetadataServiceOptions(
            EnableBangumi: true,
            PreferredLanguage:
                AppLocalizationService.Default.CurrentLanguage,
            TmdbReadAccessToken:
                Environment.GetEnvironmentVariable(
                    "EIZO_TMDB_READ_ACCESS_TOKEN"),
            CacheDirectory: cacheDirectory);

        return new MediaMetadataService(options);
    }

    private void RaiseChanged() =>
        Changed?.Invoke(this, EventArgs.Empty);
}
