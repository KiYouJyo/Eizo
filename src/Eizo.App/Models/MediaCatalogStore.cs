using System.Globalization;
using System.Text.Json;
using Eizo.Media;
using Eizo.MetadataIntegration;
using Eizo.Recognition;

namespace Eizo.Models;

public sealed class MediaCatalogStore
{
    private static readonly JsonSerializerOptions SerializerOptions =
        new() { WriteIndented = true };

    private static readonly string StorePath = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
        "Eizo",
        "catalog.json");

    private static readonly HashSet<string> SupportedVideoExtensions =
        new(StringComparer.OrdinalIgnoreCase)
        {
            ".mkv", ".mp4", ".m4v", ".mov", ".avi", ".webm",
            ".ts", ".m2ts", ".wmv", ".mpg", ".mpeg"
        };

    private static readonly MediaRecognitionService RecognitionService = new();
    private static readonly MediaIdentityBindingStore IdentityBindings =
        MediaIdentityBindingStore.Default;

    private static readonly TimeSpan[] MetadataTransportRetryDelays =
    [
        TimeSpan.FromMilliseconds(750),
        TimeSpan.FromSeconds(2),
    ];

    private static readonly TimeSpan MetadataTransportCooldownBase =
        TimeSpan.FromSeconds(5);
    private static readonly TimeSpan MetadataTransportCooldownMaximum =
        TimeSpan.FromSeconds(30);
    private const int MetadataTransportFailuresBeforeCooldown = 3;

    private readonly object _sync = new();
    private readonly SemaphoreSlim _recognitionRefreshGate = new(1, 1);
    private readonly List<CatalogMediaItemModel> _items;

    private MediaCatalogStore()
    {
        _items = LoadCore();
        if (PruneMissingLocalFilesCore())
            SaveCore(_items);
    }

    public static MediaCatalogStore Default { get; } = new();

    public event EventHandler? Changed;

    public IReadOnlyList<CatalogMediaItemModel> Snapshot()
    {
        lock (_sync)
            return _items
                .Where(IsAvailable)
                .ToArray();
    }

    public IReadOnlyList<CatalogMediaItemModel> SnapshotForSource(string sourceId)
    {
        lock (_sync)
        {
            return _items
                .Where(IsAvailable)
                .Where(item =>
                    string.Equals(
                        item.Location?.SourceId,
                        sourceId,
                        StringComparison.Ordinal))
                .ToArray();
        }
    }

    public async Task<int> EnsureRecognitionRuntimeCurrentAsync(
        CancellationToken cancellationToken = default)
    {
        await _recognitionRefreshGate.WaitAsync(cancellationToken);
        try
        {
            var currentRuntimeVersion = MediaRecognitionService.RuntimeVersion;
            CatalogMediaItemModel[] stale;

            lock (_sync)
            {
                stale = _items
                    .Where(static item => item.Recognition is not null)
                    .Where(item => !string.Equals(
                        item.Recognition!.RuntimeVersion,
                        currentRuntimeVersion,
                        StringComparison.OrdinalIgnoreCase))
                    .ToArray();
            }

            if (stale.Length == 0)
                return 0;

            var refreshed = await Task.Run(
                () =>
                {
                    var result = new Dictionary<string, CatalogMediaItemModel>(
                        stale.Length,
                        StringComparer.Ordinal);

                    foreach (var item in stale)
                    {
                        cancellationToken.ThrowIfCancellationRequested();

                        var logicalPath = item.Recognition!.LogicalPath;
                        var recognition = RecognitionService.Recognize(logicalPath);
                        result[ItemKey(item)] = item with
                        {
                            ParsedTitle = recognition.ShouldApplyDisplayTitle
                                ? recognition.Title
                                : null,
                            Meta = BuildRecognitionMeta(recognition),
                            Recognition = recognition,
                            // Metadata is provenance-bound to the Recognition
                            // snapshot that produced its provider search request.
                            // Once Recognition is refreshed, keeping the old
                            // Metadata snapshot would expose a known-invalid
                            // runtime pairing until the next enrichment pass.
                            Metadata = null,
                            Media = MediaModelProjection.Project(
                                recognition,
                                metadata: null),
                        };
                    }

                    return result;
                },
                cancellationToken);

            var changed = 0;
            lock (_sync)
            {
                for (var i = 0; i < _items.Count; i++)
                {
                    var item = _items[i];
                    if (!refreshed.TryGetValue(
                            ItemKey(item),
                            out var updated))
                    {
                        continue;
                    }

                    // A source rescan may have replaced this item while the
                    // refresh was running. Never overwrite a newer snapshot.
                    if (string.Equals(
                        item.Recognition?.RuntimeVersion,
                        currentRuntimeVersion,
                        StringComparison.OrdinalIgnoreCase))
                    {
                        continue;
                    }

                    _items[i] = updated;
                    changed++;
                }

                if (changed > 0)
                    SaveCore(_items);
            }

            if (changed > 0)
            {
                Changed?.Invoke(this, EventArgs.Empty);
                    }

            return changed;
        }
        finally
        {
            _recognitionRefreshGate.Release();
        }
    }

    internal static string ItemKey(CatalogMediaItemModel item)
    {
        var location = item.Location;
        return location is null
            ? item.Recognition?.LogicalPath ?? item.SourceTitle
            : string.Join(
                "\u001f",
                location.SourceId,
                ((int)location.Kind).ToString(CultureInfo.InvariantCulture),
                location.Locator);
    }

    public bool RegisterLocalFile(string path)
    {
        if (!IsSupportedVideoPath(path) || !File.Exists(path))
            return false;

        var fullPath = Path.GetFullPath(path);
        var fileInfo = new FileInfo(fullPath);
        var sourceId = MediaSourceStore.Default.ResolveLocalSourceId(fullPath);

        var item = CreateLocalItem(
            sourceId,
            fileInfo,
            fileInfo.Name);
        var changed = false;

        lock (_sync)
        {
            var index = _items.FindIndex(existing =>
                existing.Location is
                {
                    Kind: MediaLocationKind.LocalFile
                } location &&
                string.Equals(
                    Path.GetFullPath(location.Locator),
                    fullPath,
                    StringComparison.OrdinalIgnoreCase));

            if (index >= 0)
            {
                var existing = _items[index];
                if (CanReuseMetadata(existing, item))
                {
                    item = item with
                    {
                        Metadata = existing.Metadata,
                        Media = MediaModelProjection.Project(
                            item.Recognition,
                            existing.Metadata,
                            existing.Media),
                    };
                }

                if (existing != item)
                {
                    _items[index] = item;
                    changed = true;
                }
            }
            else
            {
                _items.Add(item);
                changed = true;
            }

            if (changed)
                SaveCore(_items);
        }

        if (changed)
        {
            Changed?.Invoke(this, EventArgs.Empty);
        }

        return true;
    }

    public Task<int> ScanSourceAsync(
        MediaSourceDefinition source,
        CancellationToken cancellationToken = default) =>
        ScanSourceAsync(
            source,
            metadataService: null,
            progress: null,
            cancellationToken);

    public Task<int> ScanSourceAsync(
        MediaSourceDefinition source,
        Action<MediaScanProgress>? progress,
        CancellationToken cancellationToken = default) =>
        ScanSourceAsync(
            source,
            metadataService: null,
            progress,
            cancellationToken);

    public async Task<int> ScanSourceAsync(
        MediaSourceDefinition source,
        MediaMetadataService? metadataService,
        Action<MediaScanProgress>? progress,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(source);

        CatalogMediaItemModel[] discovered;
        if (source.Kind == MediaSourceKind.Local)
        {
            ReportDiscoveryProgress(
                source.Id,
                directoriesProcessed: 0,
                directoriesPending: 1,
                videosDiscovered: 0,
                source.RootLocation,
                progress);

            discovered = await Task.Run(
                () => DiscoverLocalSource(source),
                cancellationToken);

            ReportDiscoveryProgress(
                source.Id,
                directoriesProcessed: 1,
                directoriesPending: 0,
                videosDiscovered: discovered.Length,
                source.RootLocation,
                progress);
        }
        else
        {
            discovered = await DiscoverRemoteSourceAsync(
                source,
                progress,
                cancellationToken);
        }

        ReuseResolvedMetadata(source.Id, discovered);

        await EnrichMetadataAsync(
            source.Id,
            discovered,
            metadataService,
            progress,
            cancellationToken);

        progress?.Invoke(
            new MediaScanProgress(
                source.Id,
                DirectoriesProcessed: 0,
                DirectoriesPending: 0,
                VideosDiscovered: discovered.Length,
                CurrentPath: null,
                Stage: MediaScanStage.Committing));

        CommitSourceScan(source.Id, discovered);

        MediaSourceStore.Default.MarkScanned(
            source.Id,
            DateTimeOffset.UtcNow);

        Changed?.Invoke(this, EventArgs.Empty);
        return discovered.Length;
    }

    public async Task<int> ScrapeSourceMetadataAsync(
        string sourceId,
        MediaMetadataService? metadataService,
        Action<MediaScanProgress>? progress = null,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(sourceId))
            return 0;

        // The standalone scrape action does not rediscover media files, so it
        // must explicitly refresh persisted Recognition snapshots before
        // Metadata consumes them. Otherwise a component update can run the
        // current Metadata runtime against Recognition output from an older
        // runtime (for example Metadata 0.2.5 + Recognition snapshot 0.2.3).
        await EnsureRecognitionRuntimeCurrentAsync(cancellationToken)
            .ConfigureAwait(false);

        CatalogMediaItemModel[] current;
        lock (_sync)
        {
            current = _items
                .Where(item =>
                    string.Equals(
                        item.Location?.SourceId,
                        sourceId,
                        StringComparison.Ordinal))
                .Where(IsAvailable)
                .ToArray();
        }

        if (current.Length == 0)
            return 0;

        var processed = await EnrichMetadataAsync(
            sourceId,
            current,
            metadataService,
            progress,
            cancellationToken,
            forceRefresh: true);

        progress?.Invoke(
            new MediaScanProgress(
                sourceId,
                DirectoriesProcessed: 0,
                DirectoriesPending: 0,
                VideosDiscovered: current.Length,
                CurrentPath: null,
                Stage: MediaScanStage.Committing,
                MetadataProcessed: processed,
                MetadataTotal: processed));

        CommitSourceScan(sourceId, current);
        Changed?.Invoke(this, EventArgs.Empty);
        return processed;
    }

    public Task<int> ScrapeMediaItemsMetadataAsync(
        IReadOnlyCollection<CatalogMediaItemModel> items,
        string progressScopeId,
        MediaMetadataService? metadataService,
        Action<MediaScanProgress>? progress = null,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(items);

        var targetKeys = items
            .Select(LocationKey)
            .Where(static key =>
                !string.IsNullOrWhiteSpace(key))
            .Select(static key => key!)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToArray();

        return ScrapeTargetMetadataAsync(
            targetKeys,
            progressScopeId,
            metadataService,
            progress,
            cancellationToken);
    }

    public Task<int> ScrapeItemMetadataAsync(
        CatalogMediaItemModel item,
        MediaMetadataService? metadataService,
        Action<MediaScanProgress>? progress = null,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(item);

        var key = LocationKey(item);
        if (string.IsNullOrWhiteSpace(key))
            return Task.FromResult(0);

        return ScrapeTargetMetadataAsync(
            [key],
            item.Media?.Id ?? key,
            metadataService,
            progress,
            cancellationToken);
    }

    private async Task<int> ScrapeTargetMetadataAsync(
        IReadOnlyCollection<string> targetKeys,
        string progressScopeId,
        MediaMetadataService? metadataService,
        Action<MediaScanProgress>? progress,
        CancellationToken cancellationToken)
    {
        if (targetKeys.Count == 0)
            return 0;

        await EnsureRecognitionRuntimeCurrentAsync(
                cancellationToken)
            .ConfigureAwait(false);

        var keySet = targetKeys.ToHashSet(
            StringComparer.OrdinalIgnoreCase);

        CatalogMediaItemModel[] current;
        lock (_sync)
        {
            current = _items
                .Where(item =>
                    LocationKey(item) is { } key &&
                    keySet.Contains(key))
                .Where(IsAvailable)
                .ToArray();
        }

        if (current.Length == 0)
            return 0;

        var processed = await EnrichMetadataAsync(
                progressScopeId,
                current,
                metadataService,
                progress,
                cancellationToken,
                forceRefresh: true)
            .ConfigureAwait(false);

        CommitTargetedMetadata(current);
        Changed?.Invoke(this, EventArgs.Empty);
        return processed;
    }

    private void CommitTargetedMetadata(
        IReadOnlyList<CatalogMediaItemModel> updatedItems)
    {
        if (updatedItems.Count == 0)
            return;

        var updatedByLocation = updatedItems
            .Select(item => (Item: item, Key: LocationKey(item)))
            .Where(static entry =>
                !string.IsNullOrWhiteSpace(entry.Key))
            .ToDictionary(
                static entry => entry.Key!,
                static entry => entry.Item,
                StringComparer.OrdinalIgnoreCase);

        if (updatedByLocation.Count == 0)
            return;

        lock (_sync)
        {
            for (var index = 0; index < _items.Count; index++)
            {
                var key = LocationKey(_items[index]);
                if (key is null ||
                    !updatedByLocation.TryGetValue(
                        key,
                        out var updated))
                {
                    continue;
                }

                _items[index] = updated;
            }

            SaveCore(_items);
        }
    }

    private static string? LocationKey(
        CatalogMediaItemModel item)
    {
        var location = item.Location;
        return location is null
            ? null
            : $"{location.SourceId}|{location.Locator}";
    }

    private async Task<CatalogMediaItemModel[]> DiscoverRemoteSourceAsync(
        MediaSourceDefinition source,
        Action<MediaScanProgress>? progress,
        CancellationToken cancellationToken)
    {
        if (!MediaSourceProviderRegistry.TryGet(
                source.Kind,
                out var provider))
        {
            throw new MediaSourceException(
                "ProviderUnavailable",
                $"No provider is registered for {source.Kind}.");
        }

        var discovered = new List<CatalogMediaItemModel>();
        var pending = new Queue<string>();
        var visited = new HashSet<string>(
            StringComparer.OrdinalIgnoreCase);
        var directoriesProcessed = 0;

        var scanRoots = source.SelectedPaths is { Count: > 0 }
            ? source.SelectedPaths
            : [string.Empty];

        foreach (var scanRoot in scanRoots)
            pending.Enqueue(scanRoot);

        ReportDiscoveryProgress(
            source.Id,
            directoriesProcessed,
            pending.Count,
            discovered.Count,
            currentPath: null,
            progress);

        while (pending.Count > 0)
        {
            cancellationToken.ThrowIfCancellationRequested();

            var relativePath = pending.Dequeue();
            if (!visited.Add(relativePath))
                continue;

            ReportDiscoveryProgress(
                source.Id,
                directoriesProcessed,
                pending.Count + 1,
                discovered.Count,
                relativePath,
                progress);

            await foreach (var entry in provider.ListAsync(
                               source,
                               relativePath,
                               cancellationToken))
            {
                cancellationToken.ThrowIfCancellationRequested();

                if (entry.IsDirectory)
                {
                    if (visited.Count + pending.Count < 100_000)
                        pending.Enqueue(entry.RelativePath);

                    continue;
                }

                var locator = entry.Locator;
                if (string.IsNullOrWhiteSpace(locator) ||
                    !IsSupportedVideoPath(locator))
                {
                    continue;
                }

                discovered.Add(
                    CreateRemoteItem(
                        source.Id,
                        entry));

                if (discovered.Count % 25 == 0)
                {
                    ReportDiscoveryProgress(
                        source.Id,
                        directoriesProcessed,
                        pending.Count + 1,
                        discovered.Count,
                        relativePath,
                        progress);
                }
            }

            directoriesProcessed++;
            ReportDiscoveryProgress(
                source.Id,
                directoriesProcessed,
                pending.Count,
                discovered.Count,
                relativePath,
                progress);
        }

        return discovered.ToArray();
    }

    private static void ReportDiscoveryProgress(
        string sourceId,
        int directoriesProcessed,
        int directoriesPending,
        int videosDiscovered,
        string? currentPath,
        Action<MediaScanProgress>? progress)
    {
        progress?.Invoke(
            new MediaScanProgress(
                sourceId,
                directoriesProcessed,
                directoriesPending,
                videosDiscovered,
                currentPath,
                Stage: MediaScanStage.Discovering));
    }

    private static CatalogMediaItemModel[] DiscoverLocalSource(
        MediaSourceDefinition source)
    {
        ArgumentNullException.ThrowIfNull(source);

        if (source.Kind != MediaSourceKind.Local ||
            string.IsNullOrWhiteSpace(source.RootLocation) ||
            !Directory.Exists(source.RootLocation))
        {
            return [];
        }

        var root = Path.GetFullPath(source.RootLocation);
        return EnumerateVideoFilesSafe(root)
            .Select(path => new FileInfo(path))
            .Where(static info => info.Exists)
            .Select(info => CreateLocalItem(
                source.Id,
                info,
                Path.GetRelativePath(root, info.FullName)))
            .ToArray();
    }

    private void ReuseResolvedMetadata(
        string sourceId,
        CatalogMediaItemModel[] discovered)
    {
        Dictionary<string, CatalogMediaItemModel> previous;
        lock (_sync)
        {
            previous = _items
                .Where(item =>
                    string.Equals(
                        item.Location?.SourceId,
                        sourceId,
                        StringComparison.Ordinal))
                .ToDictionary(
                    ItemKey,
                    StringComparer.Ordinal);
        }

        for (var i = 0; i < discovered.Length; i++)
        {
            var item = discovered[i];
            if (previous.TryGetValue(ItemKey(item), out var existing) &&
                CanReuseMetadata(existing, item))
            {
                var reusedMetadata =
                    MediaMetadataRefreshPolicy.MarkReused(
                        existing.Metadata!);
                discovered[i] = item with
                {
                    Metadata = reusedMetadata,
                    Media = MediaModelProjection.Project(
                        item.Recognition,
                        reusedMetadata,
                        existing.Media),
                };
            }
        }
    }

    private const int MetadataSubjectParallelism = 3;
    private const int MetadataSeasonParallelism = 2;

    private static async Task<int> EnrichMetadataAsync(
        string sourceId,
        CatalogMediaItemModel[] discovered,
        MediaMetadataService? metadataService,
        Action<MediaScanProgress>? progress,
        CancellationToken cancellationToken,
        bool forceRefresh = false)
    {
        if (metadataService is null || !metadataService.IsAvailable)
            return 0;

        var pending = discovered
            .Select((item, index) => (Item: item, Index: index))
            .Where(entry => forceRefresh
                ? CanAttemptMetadata(entry.Item)
                : NeedsMetadataEnrichment(entry.Item))
            .ToArray();

        if (pending.Length == 0)
        {
            progress?.Invoke(
                new MediaScanProgress(
                    sourceId,
                    DirectoriesProcessed: 0,
                    DirectoriesPending: 0,
                    VideosDiscovered: discovered.Length,
                    CurrentPath: null,
                    Stage: MediaScanStage.Metadata,
                    MetadataProcessed: 0,
                    MetadataTotal: 0,
                    MetadataResolved: 0,
                    MetadataUnresolved: 0,
                    MetadataErrors: 0));
            return 0;
        }

        // v0.5.14 schedules metadata by logical subject instead of by file.
        // The representative item establishes the persisted TMDB identity.
        // Remaining episodes then reuse the exact subject ID; different
        // subjects are allowed to make progress concurrently.
        var subjectBatches = pending
            .GroupBy(
                entry => MetadataSubjectKey(entry.Item),
                StringComparer.OrdinalIgnoreCase)
            .Select(group => group
                .OrderBy(entry =>
                    entry.Item.Recognition?.SeasonNumber ??
                    int.MaxValue)
                .ThenBy(entry =>
                    entry.Item.Recognition?.EpisodeNumber ??
                    entry.Item.Recognition?.SpecialNumber ??
                    decimal.MaxValue)
                .ThenBy(entry => entry.Index)
                .ToArray())
            .ToArray();

        var processed = 0;
        var resolved = 0;
        var unresolved = 0;
        var errors = 0;
        var progressSync = new object();

        ReportMetadataProgress();

        using var subjectGate =
            new SemaphoreSlim(
                MetadataSubjectParallelism,
                MetadataSubjectParallelism);

        var subjectTasks = subjectBatches.Select(
            async batch =>
            {
                await subjectGate.WaitAsync(
                        cancellationToken)
                    .ConfigureAwait(false);
                try
                {
                    await ProcessSubjectBatchAsync(batch)
                        .ConfigureAwait(false);
                }
                finally
                {
                    subjectGate.Release();
                }
            });

        await Task.WhenAll(subjectTasks)
            .ConfigureAwait(false);

        return processed;

        async Task ProcessSubjectBatchAsync(
            (CatalogMediaItemModel Item, int Index)[] batch)
        {
            if (batch.Length == 0)
                return;

            // Resolve one representative first. UpsertAutomatic persists the
            // TMDB subject ID before follower episodes are scheduled.
            await ProcessEntryAsync(batch[0])
                .ConfigureAwait(false);

            if (batch.Length == 1)
                return;

            var seasonBatches = batch
                .Skip(1)
                .GroupBy(entry =>
                    entry.Item.Recognition?.SeasonNumber ??
                    0)
                .Select(static group =>
                    group.ToArray())
                .ToArray();

            using var seasonGate =
                new SemaphoreSlim(
                    MetadataSeasonParallelism,
                    MetadataSeasonParallelism);

            var seasonTasks = seasonBatches.Select(
                async seasonBatch =>
                {
                    await seasonGate.WaitAsync(
                            cancellationToken)
                        .ConfigureAwait(false);
                    try
                    {
                        // Keep episodes inside one season ordered. The first
                        // episode warms the TMDB season cache; following
                        // episodes become local cache lookups plus mapping.
                        foreach (var entry in seasonBatch)
                        {
                            await ProcessEntryAsync(entry)
                                .ConfigureAwait(false);
                        }
                    }
                    finally
                    {
                        seasonGate.Release();
                    }
                });

            await Task.WhenAll(seasonTasks)
                .ConfigureAwait(false);
        }

        async Task ProcessEntryAsync(
            (CatalogMediaItemModel Item, int Index) entry)
        {
            cancellationToken.ThrowIfCancellationRequested();

            var recognition = entry.Item.Recognition!;
            var binding = IdentityBindings.GetTmdbHint(
                entry.Item.Media?.Id);
            var candidateMetadata =
                await EnrichMetadataWithTransportRetryAsync(
                        metadataService,
                        recognition,
                        binding,
                        cancellationToken)
                    .ConfigureAwait(false);
            var metadata = MediaMetadataRefreshPolicy.Select(
                entry.Item.Metadata,
                candidateMetadata,
                forceRefresh,
                DateTimeOffset.UtcNow);

            var status = metadata?.Status;
            if (metadata is not null)
            {
                discovered[entry.Index] = entry.Item with
                {
                    Metadata = metadata,
                    Media = MediaModelProjection.Project(
                        recognition,
                        metadata,
                        entry.Item.Media),
                };

                if (metadata.IsResolved)
                {
                    IdentityBindings.UpsertAutomatic(
                        entry.Item.Media?.Id,
                        metadata);
                }
            }

            lock (progressSync)
            {
                processed++;

                switch (status)
                {
                    case MediaMetadataStatus.Resolved:
                        resolved++;
                        break;
                    case MediaMetadataStatus.Error:
                        errors++;
                        break;
                    default:
                        unresolved++;
                        break;
                }

                ReportMetadataProgress();
            }
        }

        void ReportMetadataProgress() =>
            progress?.Invoke(
                new MediaScanProgress(
                    sourceId,
                    DirectoriesProcessed: 0,
                    DirectoriesPending: 0,
                    VideosDiscovered: discovered.Length,
                    CurrentPath: null,
                    Stage: MediaScanStage.Metadata,
                    MetadataProcessed: processed,
                    MetadataTotal: pending.Length,
                    MetadataResolved: resolved,
                    MetadataUnresolved: unresolved,
                    MetadataErrors: errors));
    }

    private static string MetadataSubjectKey(
        CatalogMediaItemModel item)
    {
        if (!string.IsNullOrWhiteSpace(item.Media?.Id))
            return item.Media.Id;

        var recognition = item.Recognition;
        return string.Join(
            "|",
            recognition?.MediaKind ?? string.Empty,
            recognition?.Title?.Trim()
                .ToUpperInvariant() ?? string.Empty,
            recognition?.Year?.ToString(
                CultureInfo.InvariantCulture) ??
            string.Empty);
    }

    private static async Task<MediaMetadataSnapshot?> EnrichMetadataWithTransportRetryAsync(
        MediaMetadataService metadataService,
        MediaRecognitionSnapshot recognition,
        MediaIdentityBindingHint? binding,
        CancellationToken cancellationToken)
    {
        MediaMetadataSnapshot? last = null;

        for (var attempt = 0;
             attempt <= MetadataTransportRetryDelays.Length;
             attempt++)
        {
            cancellationToken.ThrowIfCancellationRequested();

            try
            {
                last = await metadataService
                    .EnrichAsync(
                        recognition,
                        binding,
                        cancellationToken)
                    .ConfigureAwait(false);
            }
            catch (OperationCanceledException)
                when (cancellationToken.IsCancellationRequested)
            {
                throw;
            }
            catch (Exception exception)
            {
                last = CreateMetadataFailureSnapshot(
                    recognition,
                    exception.GetType().Name,
                    exception.Message);
            }

            if (last is null ||
                !IsTransportMetadataFailure(last) ||
                attempt == MetadataTransportRetryDelays.Length)
            {
                return last;
            }

            await Task.Delay(
                    MetadataTransportRetryDelays[attempt],
                    cancellationToken)
                .ConfigureAwait(false);
        }

        return last;
    }

    private static TimeSpan MetadataTransportCooldown(int cooldownLevel)
    {
        var multiplier = 1 << Math.Min(Math.Max(0, cooldownLevel), 3);
        var milliseconds = Math.Min(
            MetadataTransportCooldownMaximum.TotalMilliseconds,
            MetadataTransportCooldownBase.TotalMilliseconds * multiplier);

        return TimeSpan.FromMilliseconds(milliseconds);
    }

    private static MediaMetadataSnapshot CreateMetadataFailureSnapshot(
        MediaRecognitionSnapshot recognition,
        string errorType,
        string message) =>
        new(
            RuntimeVersion: MediaMetadataService.RuntimeVersion,
            RecognitionRuntimeVersion: recognition.RuntimeVersion,
            Status: MediaMetadataStatus.Error,
            Provider: null,
            ProviderSubjectId: null,
            SubjectKind: null,
            CanonicalTitle: null,
            OriginalTitle: null,
            LocalizedTitles: new Dictionary<string, string>(
                StringComparer.OrdinalIgnoreCase),
            Aliases: [],
            Overview: null,
            ReleaseDate: null,
            EpisodeCount: null,
            PosterUrl: null,
            BackdropUrl: null,
            ExternalIds: new Dictionary<string, string>(
                StringComparer.OrdinalIgnoreCase),
            EpisodeNumber:
                recognition.EpisodeNumber ??
                recognition.SpecialNumber,
            EpisodeTitle: null,
            EpisodeOriginalTitle: null,
            EpisodeOverview: null,
            EpisodeAirDate: null,
            EpisodeThumbnailUrl: null,
            Confidence: 0,
            Errors:
            [
                new MetadataProviderErrorSnapshot(
                    "metadata",
                    errorType,
                    message)
            ],
            UpdatedAtUtc: DateTimeOffset.UtcNow);

    private static bool IsTransportMetadataFailure(
        MediaMetadataSnapshot metadata) =>
        MediaMetadataRefreshPolicy.IsTransportFailure(
            metadata);

    private static bool CanAttemptMetadata(
        CatalogMediaItemModel item) =>
        item.Recognition is
        {
            Status: MediaRecognitionStatus.Recognized,
            Title.Length: > 0,
            ConfidenceLevel: "Medium" or "High",
        };

    private static bool NeedsMetadataEnrichment(
        CatalogMediaItemModel item)
    {
        if (!CanAttemptMetadata(item))
            return false;

        var recognition = item.Recognition!;

        if (item.Metadata is
            {
                IsResolved: true,
            } metadata)
        {
            // v0.5.14 upgrades legacy Bangumi-led library snapshots into
            // the TMDB-only authority even when runtime versions still match.
            if (!string.Equals(
                    metadata.Provider,
                    "tmdb",
                    StringComparison.OrdinalIgnoreCase))
            {
                return true;
            }

            if (string.Equals(
                    metadata.RuntimeVersion,
                    MediaMetadataService.RuntimeVersion,
                    StringComparison.OrdinalIgnoreCase) &&
                metadata.MatchesRecognitionRuntime(
                    recognition.RuntimeVersion))
            {
                return false;
            }
        }

        return true;
    }

    private void CommitSourceScan(
        string sourceId,
        IReadOnlyList<CatalogMediaItemModel> discovered)
    {
        lock (_sync)
        {
            _items.RemoveAll(item =>
                string.Equals(
                    item.Location?.SourceId,
                    sourceId,
                    StringComparison.Ordinal));

            _items.AddRange(discovered);
            SaveCore(_items);
        }
    }

    public int ScanLocalSource(MediaSourceDefinition source)
    {
        var discovered = DiscoverLocalSource(source);
        ReuseResolvedMetadata(source.Id, discovered);
        CommitSourceScan(source.Id, discovered);

        MediaSourceStore.Default.MarkScanned(
            source.Id,
            DateTimeOffset.UtcNow);

        Changed?.Invoke(this, EventArgs.Empty);
        return discovered.Length;
    }

    public void SetManualIdentityBinding(
        string eizoMediaId,
        string provider,
        string providerSubjectId,
        bool makePrimary = true)
    {
        IdentityBindings.SetManual(
            eizoMediaId,
            provider,
            providerSubjectId,
            makePrimary);
        InvalidateMetadataForMedia(eizoMediaId);
    }

    public void ClearManualIdentityBinding(
        string eizoMediaId,
        string provider,
        bool removeExternalId = false)
    {
        IdentityBindings.ClearManual(
            eizoMediaId,
            provider,
            removeExternalId);
        InvalidateMetadataForMedia(eizoMediaId);
    }

    public MediaIdentityBindingHint? GetIdentityBinding(
        string eizoMediaId) =>
        IdentityBindings.GetHint(eizoMediaId);

    private void InvalidateMetadataForMedia(
        string eizoMediaId)
    {
        var changed = false;
        lock (_sync)
        {
            for (var index = 0; index < _items.Count; index++)
            {
                var item = _items[index];
                if (!string.Equals(
                        item.Media?.Id,
                        eizoMediaId,
                        StringComparison.OrdinalIgnoreCase))
                {
                    continue;
                }

                _items[index] = item with
                {
                    Metadata = null,
                    Media = MediaModelProjection.Project(
                        item.Recognition,
                        metadata: null,
                        item.Media),
                };
                changed = true;
            }

            if (changed)
                SaveCore(_items);
        }

        if (changed)
            Changed?.Invoke(this, EventArgs.Empty);
    }

    public void RemoveSourceItems(string sourceId)
    {
        bool changed;

        lock (_sync)
        {
            changed = _items.RemoveAll(item =>
                string.Equals(
                    item.Location?.SourceId,
                    sourceId,
                    StringComparison.Ordinal)) > 0;

            if (changed)
                SaveCore(_items);
        }

        if (changed)
            Changed?.Invoke(this, EventArgs.Empty);
    }

    public static bool IsSupportedVideoPath(string? path)
    {
        if (string.IsNullOrWhiteSpace(path))
            return false;

        var candidate = path;

        if (Uri.TryCreate(path, UriKind.Absolute, out var uri) &&
            !uri.IsFile)
        {
            candidate = Uri.UnescapeDataString(uri.AbsolutePath);
        }

        return SupportedVideoExtensions.Contains(
            Path.GetExtension(candidate));
    }

    private static CatalogMediaItemModel CreateRemoteItem(
        string sourceId,
        MediaSourceEntry entry)
    {
        var title = Path.GetFileNameWithoutExtension(
            entry.Name);

        if (string.IsNullOrWhiteSpace(title))
            title = entry.Name;

        var logicalPath = string.IsNullOrWhiteSpace(entry.RelativePath)
            ? entry.Name
            : entry.RelativePath;
        var recognition = RecognitionService.Recognize(logicalPath);

        return new CatalogMediaItemModel(
            title,
            recognition.ShouldApplyDisplayTitle
                ? recognition.Title
                : null,
            NativeTitle: null,
            Category: null,
            Meta: BuildRecognitionMeta(recognition),
            Location: new MediaLocationModel(
                sourceId,
                MediaLocationKind.RemoteUri,
                entry.Locator!,
                entry.SizeBytes,
                entry.ModifiedUtc),
            Recognition: recognition,
            Media: MediaModelProjection.Project(
                recognition,
                metadata: null));
    }

    private static CatalogMediaItemModel CreateLocalItem(
        string sourceId,
        FileInfo fileInfo,
        string logicalPath)
    {
        var recognition = RecognitionService.Recognize(logicalPath);

        return new CatalogMediaItemModel(
            Path.GetFileNameWithoutExtension(fileInfo.Name),
            recognition.ShouldApplyDisplayTitle
                ? recognition.Title
                : null,
            NativeTitle: null,
            Category: null,
            Meta: BuildRecognitionMeta(recognition),
            Location: new MediaLocationModel(
                sourceId,
                MediaLocationKind.LocalFile,
                fileInfo.FullName,
                fileInfo.Length,
                fileInfo.LastWriteTimeUtc),
            Recognition: recognition,
            Media: MediaModelProjection.Project(
                recognition,
                metadata: null));
    }

    private static string BuildRecognitionMeta(
        MediaRecognitionSnapshot recognition)
    {
        var parts = new List<string>();

        if (recognition.SeasonNumber is { } season &&
            recognition.EpisodeNumber is { } seasonEpisode)
        {
            parts.Add($"S{season:00}E{FormatNumber(seasonEpisode)}");
        }
        else if (recognition.EpisodeNumber is { } episode)
        {
            parts.Add($"EP{FormatNumber(episode)}");
        }
        else if (recognition.SpecialNumber is { } special)
        {
            parts.Add($"SP{FormatNumber(special)}");
        }

        if (!string.IsNullOrWhiteSpace(recognition.EpisodeTitle))
            parts.Add(recognition.EpisodeTitle!);

        if (recognition.Year is { } year)
            parts.Add(year.ToString(CultureInfo.InvariantCulture));

        parts.Add(recognition.Status switch
        {
            MediaRecognitionStatus.Recognized => recognition.ConfidenceLevel,
            MediaRecognitionStatus.Ambiguous => "Ambiguous",
            MediaRecognitionStatus.Unresolved => "Unresolved",
            MediaRecognitionStatus.Error =>
                string.IsNullOrWhiteSpace(recognition.ErrorCode)
                    ? "Recognition error"
                    : $"Recognition error: {recognition.ErrorCode}",
            _ => recognition.Status.ToString()
        });

        return string.Join(" · ", parts);
    }

    private static string FormatNumber(decimal value) =>
        value.ToString("0.###", CultureInfo.InvariantCulture);

    private static IEnumerable<string> EnumerateVideoFilesSafe(string root)
    {
        var pending = new Stack<string>();
        pending.Push(Path.GetFullPath(root));

        while (pending.Count > 0)
        {
            var current = pending.Pop();

            IEnumerable<string> files;
            try
            {
                files = Directory.EnumerateFiles(current).ToArray();
            }
            catch (IOException)
            {
                files = [];
            }
            catch (UnauthorizedAccessException)
            {
                files = [];
            }

            foreach (var file in files)
            {
                if (IsSupportedVideoPath(file))
                    yield return file;
            }

            IEnumerable<string> directories;
            try
            {
                directories = Directory.EnumerateDirectories(current).ToArray();
            }
            catch (IOException)
            {
                directories = [];
            }
            catch (UnauthorizedAccessException)
            {
                directories = [];
            }

            foreach (var directory in directories)
            {
                try
                {
                    if ((File.GetAttributes(directory) &
                         FileAttributes.ReparsePoint) != 0)
                    {
                        continue;
                    }
                }
                catch (IOException)
                {
                    continue;
                }
                catch (UnauthorizedAccessException)
                {
                    continue;
                }

                pending.Push(directory);
            }
        }
    }

    private static bool CanReuseMetadata(
        CatalogMediaItemModel existing,
        CatalogMediaItemModel discovered)
    {
        if (existing.Metadata is not
            {
                IsResolved: true,
            } metadata ||
            !string.Equals(
                metadata.Provider,
                "tmdb",
                StringComparison.OrdinalIgnoreCase) ||
            existing.Recognition is null ||
            discovered.Recognition is null ||
            !string.Equals(
                metadata.RuntimeVersion,
                MediaMetadataService.RuntimeVersion,
                StringComparison.OrdinalIgnoreCase) ||
            !metadata.MatchesRecognitionRuntime(
                discovered.Recognition.RuntimeVersion))
        {
            return false;
        }

        var left = existing.Recognition;
        var right = discovered.Recognition;

        return string.Equals(left.Title, right.Title, StringComparison.OrdinalIgnoreCase) &&
               string.Equals(left.MediaKind, right.MediaKind, StringComparison.OrdinalIgnoreCase) &&
               left.Year == right.Year &&
               left.SeasonNumber == right.SeasonNumber &&
               left.EpisodeNumber == right.EpisodeNumber &&
               left.SpecialNumber == right.SpecialNumber;
    }

    private static bool IsAvailable(CatalogMediaItemModel item) =>
        item.Location switch
        {
            { Kind: MediaLocationKind.LocalFile } location =>
                File.Exists(location.Locator),
            { Kind: MediaLocationKind.RemoteUri } =>
                true,
            null =>
                false,
            _ =>
                false
        };

    private bool PruneMissingLocalFilesCore()
    {
        return _items.RemoveAll(item =>
            item.Location is
            {
                Kind: MediaLocationKind.LocalFile
            } location &&
            !File.Exists(location.Locator)) > 0;
    }

    private static List<CatalogMediaItemModel> LoadCore()
    {
        try
        {
            if (!File.Exists(StorePath))
                return [];

            var document =
                JsonSerializer.Deserialize<MediaCatalogStoreDocument>(
                    File.ReadAllText(StorePath),
                    SerializerOptions);

            if (document is not { SchemaVersion: 1 or 2 or 3 })
                return [];

            var items = document.Items ?? [];
            for (var index = 0; index < items.Count; index++)
            {
                var item = items[index];
                if (item.Media is null && item.Recognition is not null)
                {
                    items[index] = item with
                    {
                        Media = MediaModelProjection.Project(
                            item.Recognition,
                            item.Metadata),
                    };
                }
            }

            return items;
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

    private static void SaveCore(IReadOnlyList<CatalogMediaItemModel> items)
    {
        try
        {
            Directory.CreateDirectory(Path.GetDirectoryName(StorePath)!);
            var temporaryPath =
                $"{StorePath}.{Environment.ProcessId}.{Guid.NewGuid():N}.tmp";

            var document = new MediaCatalogStoreDocument(
                SchemaVersion: 3,
                Items: items.ToList());

            File.WriteAllText(
                temporaryPath,
                JsonSerializer.Serialize(document, SerializerOptions));

            File.Move(temporaryPath, StorePath, overwrite: true);
        }
        catch (IOException)
        {
        }
        catch (UnauthorizedAccessException)
        {
        }
    }

    private sealed record MediaCatalogStoreDocument(
        int SchemaVersion,
        List<CatalogMediaItemModel> Items);
}
