using System.Collections.ObjectModel;
using System.Globalization;
using Eizo.Cache;
using Eizo.Localization;
using Eizo.Models;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;

namespace Eizo.Views;

public sealed partial class CacheView : UserControl
{
    private static readonly long[] CacheLimits =
    [
        8L * 1024 * 1024 * 1024,
        16L * 1024 * 1024 * 1024,
        32L * 1024 * 1024 * 1024,
        64L * 1024 * 1024 * 1024,
        128L * 1024 * 1024 * 1024
    ];

    private static readonly long[] PrecacheSizes =
    [
        64L * 1024 * 1024,
        128L * 1024 * 1024,
        256L * 1024 * 1024,
        512L * 1024 * 1024,
        1024L * 1024 * 1024
    ];

    private readonly AppLocalizationService _localization =
        AppLocalizationService.Default;
    private readonly ObservableCollection<CacheItemModel> _items = [];

    private CacheSnapshot? _lastSnapshot;
    private DateTimeOffset _lastSnapshotUtc;
    private bool _isSynchronizing;
    private bool _busy;
    private bool _downloadEventsAttached;
    private bool _hasActiveDownloads;

    public CacheView()
    {
        _isSynchronizing = true;
        InitializeComponent();

        CacheList.ItemsSource = _items;
        ApplyText();

        Loaded += CacheView_Loaded;
        Unloaded += CacheView_Unloaded;
    }

    public event EventHandler<CachedVideoPlaybackRequest>? PlaybackRequested;

    private string T(string key) =>
        _localization.GetString(key);

    private async void CacheView_Loaded(
        object sender,
        RoutedEventArgs e)
    {
        AttachDownloadEvents();
        SynchronizePolicyControls();

        await CacheRuntime.RunStartupMaintenanceAsync();
        await RefreshAsync();
    }

    private void CacheView_Unloaded(
        object sender,
        RoutedEventArgs e) =>
        DetachDownloadEvents();

    private void AttachDownloadEvents()
    {
        if (_downloadEventsAttached)
            return;

        VideoCacheDownloadManager.Default.Changed +=
            DownloadManager_Changed;
        _downloadEventsAttached = true;
    }

    private void DetachDownloadEvents()
    {
        if (!_downloadEventsAttached)
            return;

        VideoCacheDownloadManager.Default.Changed -=
            DownloadManager_Changed;
        _downloadEventsAttached = false;
    }

    private void DownloadManager_Changed(
        object? sender,
        EventArgs e)
    {
        DispatcherQueue.TryEnqueue(
            () =>
            {
                if (_lastSnapshot is not null)
                    ApplySnapshot(_lastSnapshot);

                var tasks =
                    VideoCacheDownloadManager.Default.Snapshot();
                var terminalChanged =
                    tasks.Any(task =>
                        (task.Status is
                            VideoCacheDownloadStatus.Completed or
                            VideoCacheDownloadStatus.Failed or
                            VideoCacheDownloadStatus.Canceled) &&
                        task.UpdatedUtc >
                            _lastSnapshotUtc);

                if (terminalChanged ||
                    DateTimeOffset.UtcNow -
                    _lastSnapshotUtc >
                    TimeSpan.FromSeconds(2))
                {
                    _ = RefreshAsync();
                }
            });
    }

    private void SynchronizePolicyControls()
    {
        _isSynchronizing = true;
        try
        {
            var settings = AppSettingsStore.Current;

            AutoCleanupToggle.IsOn =
                settings.CacheAutoCleanup;
            KeepOfflineToggle.IsOn =
                settings.PreserveOfflineCache;

            CacheLimitCombo.SelectedIndex =
                FindClosestIndex(
                    CacheLimits,
                    settings.CacheLimitBytes > 0
                        ? settings.CacheLimitBytes
                        : CacheDefaults.LimitBytes);

            PrecacheSizeCombo.SelectedIndex =
                FindClosestIndex(
                    PrecacheSizes,
                    settings.RemotePrecacheBytes > 0
                        ? settings.RemotePrecacheBytes
                        : CacheDefaults.RemotePrecacheBytes);
        }
        finally
        {
            _isSynchronizing = false;
        }
    }

    private async Task RefreshAsync()
    {
        if (_busy)
            return;

        SetBusy(true);

        try
        {
            var snapshot =
                await CacheRuntime.Store.GetSnapshotAsync();

            _lastSnapshot = snapshot;
            _lastSnapshotUtc =
                DateTimeOffset.UtcNow;

            ApplySnapshot(snapshot);
            CacheStatusText.Visibility =
                Visibility.Collapsed;
        }
        catch
        {
            CacheStatusText.Text =
                T("Cache_StatusError");
            CacheStatusText.Visibility =
                Visibility.Visible;
        }
        finally
        {
            SetBusy(false);
        }
    }

    private void ApplySnapshot(
        CacheSnapshot snapshot)
    {
        var desired =
            BuildDesiredItems(
                snapshot);

        ReconcileItems(
            desired);

        var tasks =
            VideoCacheDownloadManager.Default.Snapshot();

        _hasActiveDownloads =
            tasks.Any(static task =>
                task.Status is
                    VideoCacheDownloadStatus.Downloading or
                    VideoCacheDownloadStatus.Paused);

        var taskGroupKeys =
            tasks
                .Where(static task =>
                    task.Status !=
                        VideoCacheDownloadStatus.Canceled &&
                    !string.IsNullOrWhiteSpace(
                        task.GroupKey))
                .Select(static task =>
                    task.GroupKey!)
                .ToHashSet(
                    StringComparer.Ordinal);

        var pinnedGroupKeys =
            snapshot.Entries
                .Where(static entry =>
                    entry.Category ==
                        CacheCategory.Media &&
                    entry.Pinned &&
                    !string.IsNullOrWhiteSpace(
                        entry.GroupKey))
                .Select(static entry =>
                    entry.GroupKey!)
                .ToHashSet(
                    StringComparer.Ordinal);

        var videoGroupKeys =
            new HashSet<string>(
                pinnedGroupKeys,
                StringComparer.Ordinal);
        videoGroupKeys.UnionWith(
            taskGroupKeys);

        var videoEntries =
            snapshot.Entries
                .Where(entry =>
                    entry.Category ==
                        CacheCategory.Media &&
                    !string.IsNullOrWhiteSpace(
                        entry.GroupKey) &&
                    videoGroupKeys.Contains(
                        entry.GroupKey!))
                .ToArray();

        var videoCacheBytes =
            videoEntries.Sum(
                static entry =>
                    entry.SizeBytes);
        var applicationCacheBytes =
            Math.Max(
                0,
                snapshot.TotalBytes -
                videoCacheBytes);

        var limit =
            Math.Max(
                1,
                CacheRuntime.CurrentPolicy.LimitBytes);
        var usage =
            Math.Clamp(
                snapshot.TotalBytes /
                (double)limit *
                100d,
                0d,
                100d);

        OverviewUsedText.Text =
            FormatBytes(snapshot.TotalBytes);
        OverviewLimit.Text =
            string.Format(
                CultureInfo.CurrentCulture,
                T("Cache_OverviewLimit"),
                FormatBytes(limit));

        CacheUsageProgress.Value = usage;
        UsagePercentText.Text =
            usage.ToString(
                "0.0",
                CultureInfo.CurrentCulture) +
            "%";

        VideoCacheSizeText.Text =
            T("Cache_VideoCache") +
            " " +
            FormatBytes(videoCacheBytes);
        ApplicationCacheSizeText.Text =
            T("Cache_ApplicationCache") +
            " " +
            FormatBytes(applicationCacheBytes);
        ApplicationCacheValue.Text =
            FormatBytes(applicationCacheBytes);

        EmptyStateText.Visibility =
            _items.Count == 0
                ? Visibility.Visible
                : Visibility.Collapsed;

        UpdateActionAvailability();
    }

    private IReadOnlyList<CacheItemModel> BuildDesiredItems(
        CacheSnapshot snapshot)
    {
        var desired =
            new List<CacheItemModel>();

        var tasks =
            VideoCacheDownloadManager.Default.Snapshot();

        var shownGroups =
            new HashSet<string>(
                StringComparer.Ordinal);

        foreach (var task in tasks.Where(
                     static task =>
                         task.Status !=
                         VideoCacheDownloadStatus.Canceled))
        {
            var completed =
                task.Status ==
                VideoCacheDownloadStatus.Completed &&
                !string.IsNullOrWhiteSpace(
                    task.GroupKey);
            var paused =
                task.Status ==
                VideoCacheDownloadStatus.Paused;
            var failed =
                task.Status ==
                VideoCacheDownloadStatus.Failed;

            if (completed &&
                task.GroupKey is { } completedGroup)
            {
                shownGroups.Add(
                    completedGroup);
            }

            var total =
                Math.Max(
                    0,
                    task.TotalBytes);
            var completedBytes =
                Math.Clamp(
                    task.CompletedBytes,
                    0,
                    total > 0
                        ? total
                        : long.MaxValue);

            var status =
                task.Status switch
                {
                    VideoCacheDownloadStatus.Completed =>
                        T("Cache_StatusCompleted"),
                    VideoCacheDownloadStatus.Paused =>
                        T("Cache_StatusPaused"),
                    VideoCacheDownloadStatus.Failed =>
                        T("Cache_StatusFailed"),
                    _ =>
                        T("Cache_StatusDownloading")
                };

            var progressText =
                total > 0
                    ? FormatBytes(completedBytes) +
                      " / " +
                      FormatBytes(total) +
                      " · " +
                      task.ProgressPercent.ToString(
                          "0",
                          CultureInfo.CurrentCulture) +
                      "%"
                    : T("Cache_StatusPreparing");

            desired.Add(
                new CacheItemModel(
                    "task:" + task.TaskKey,
                    task.Title,
                    task.Source,
                    total > 0
                        ? FormatBytes(total)
                        : "—",
                    task.UpdatedUtc
                        .ToLocalTime()
                        .ToString(
                            "g",
                            CultureInfo.CurrentCulture),
                    completed
                        ? 100d
                        : task.ProgressPercent,
                    progressText,
                    status,
                    completed,
                    task.GroupKey,
                    task.TaskKey,
                    completed &&
                    task.GroupKey is { } groupKey
                        ? new CachedVideoPlaybackRequest(
                            groupKey,
                            task.Title,
                            task.Source,
                            total)
                        : null,
                    T("Cache_DeleteVideo"),
                    paused,
                    failed));
        }

        foreach (var group in snapshot.Entries
                     .Where(static entry =>
                         entry.Category ==
                             CacheCategory.Media &&
                         entry.Pinned &&
                         !string.IsNullOrWhiteSpace(
                             entry.GroupKey))
                     .GroupBy(
                         static entry =>
                             entry.GroupKey!,
                         StringComparer.Ordinal)
                     .Where(group =>
                         !shownGroups.Contains(
                             group.Key)))
        {
            var entries =
                group.ToArray();
            var newest =
                entries.Max(
                    static entry =>
                        entry.LastAccessedUtc);
            var first =
                entries[0];
            var size =
                entries.Sum(
                    static entry =>
                        entry.SizeBytes);
            var episodeLabel =
                ResolveCachedEpisodeLabel(
                    group.Key,
                    first.Source);

            desired.Add(
                new CacheItemModel(
                    "group:" + group.Key,
                    first.DisplayName,
                    episodeLabel,
                    FormatBytes(size),
                    newest
                        .ToLocalTime()
                        .ToString(
                            "g",
                            CultureInfo.CurrentCulture),
                    100d,
                    FormatBytes(size),
                    T("Cache_StatusCompleted"),
                    true,
                    group.Key,
                    taskKey: null,
                    playbackRequest: new CachedVideoPlaybackRequest(
                        group.Key,
                        first.DisplayName,
                        episodeLabel,
                        size),
                    deleteText: T("Cache_DeleteVideo")));
        }

        return desired;
    }

    private string ResolveCachedEpisodeLabel(
        string groupKey,
        string fallback)
    {
        if (!WebDavMediaCacheKeys.TryParseGroupKey(
                groupKey,
                out var sourceId,
                out var mediaUri) ||
            mediaUri is null)
        {
            return fallback;
        }

        var item =
            MediaCatalogStore.Default
                .SnapshotForSource(sourceId)
                .FirstOrDefault(candidate =>
                {
                    if (candidate.Location is not
                        {
                            Kind: MediaLocationKind.RemoteUri
                        } location ||
                        !Uri.TryCreate(
                            location.Locator,
                            UriKind.Absolute,
                            out var locator))
                    {
                        return false;
                    }

                    return Uri.Compare(
                               locator,
                               mediaUri,
                               UriComponents.AbsoluteUri,
                               UriFormat.SafeUnescaped,
                               StringComparison.OrdinalIgnoreCase) ==
                           0;
                });

        if (item?.Recognition?.EpisodeNumber is not
            { } episodeNumber)
        {
            return fallback;
        }

        var number =
            episodeNumber ==
            decimal.Truncate(episodeNumber)
                ? decimal.Truncate(episodeNumber)
                    .ToString(
                        CultureInfo.CurrentCulture)
                : episodeNumber.ToString(
                    "0.##",
                    CultureInfo.CurrentCulture);

        return _localization.CurrentLanguage switch
        {
            "ja-JP" => $"第{number}話",
            "en-US" => $"Episode {number}",
            _ => $"第 {number} 集",
        };
    }

    private void ReconcileItems(
        IReadOnlyList<CacheItemModel> desired)
    {
        var desiredIds =
            desired
                .Select(static item =>
                    item.Id)
                .ToHashSet(
                    StringComparer.Ordinal);

        foreach (var candidate in desired)
        {
            var existing =
                _items.FirstOrDefault(
                    item =>
                        string.Equals(
                            item.Id,
                            candidate.Id,
                            StringComparison.Ordinal));

            if (existing is null)
            {
                _items.Add(candidate);
                continue;
            }

            existing.UpdateFrom(candidate);
        }

        for (var index =
                 _items.Count - 1;
             index >= 0;
             index--)
        {
            if (!desiredIds.Contains(
                    _items[index].Id))
            {
                _items.RemoveAt(index);
            }
        }
    }

    private void CacheList_ItemClick(
        object sender,
        ItemClickEventArgs e)
    {
        if (e.ClickedItem is not CacheItemModel item)
            return;

        if (item.IsCompleted &&
            item.PlaybackRequest is { } request)
        {
            PlaybackRequested?.Invoke(
                this,
                request);
            return;
        }

        if (item.TaskKey is { } taskKey &&
            !item.IsFailed)
        {
            VideoCacheDownloadManager.Default.TogglePause(
                taskKey);
        }
    }

    private async void ClearApplicationCacheButton_Click(
        object sender,
        RoutedEventArgs e)
    {
        if (_busy ||
            _hasActiveDownloads)
        {
            return;
        }

        SetBusy(true);
        try
        {
            await CacheRuntime.Store.ClearAsync(
                preservePinned: true);
        }
        finally
        {
            SetBusy(false);
        }

        await RefreshAsync();
    }

    private async void DeleteCacheItem_Click(
        object sender,
        RoutedEventArgs e)
    {
        if (_busy ||
            sender is not FrameworkElement
            {
                Tag: string id
            })
        {
            return;
        }

        SetBusy(true);
        try
        {
            const string taskPrefix = "task:";
            const string groupPrefix = "group:";

            if (id.StartsWith(
                    taskPrefix,
                    StringComparison.Ordinal))
            {
                await VideoCacheDownloadManager.Default.DeleteTaskAsync(
                    id[taskPrefix.Length..]);
            }
            else if (id.StartsWith(
                         groupPrefix,
                         StringComparison.Ordinal))
            {
                await VideoCacheDownloadManager.Default.DeleteGroupAsync(
                    id[groupPrefix.Length..]);
            }
        }
        finally
        {
            SetBusy(false);
        }

        await RefreshAsync();
    }

    private async void AutoCleanupToggle_Toggled(
        object sender,
        RoutedEventArgs e)
    {
        if (_isSynchronizing)
            return;

        AppSettingsStore.Update(
            settings => settings with
            {
                CacheAutoCleanup =
                    AutoCleanupToggle.IsOn
            });

        if (AutoCleanupToggle.IsOn)
        {
            await CacheRuntime.EnforcePolicyAsync();
            await RefreshAsync();
        }
    }

    private async void CacheLimitCombo_SelectionChanged(
        object sender,
        SelectionChangedEventArgs e)
    {
        if (_isSynchronizing ||
            CacheLimitCombo.SelectedIndex < 0 ||
            CacheLimitCombo.SelectedIndex >=
            CacheLimits.Length)
        {
            return;
        }

        var limit =
            CacheLimits[
                CacheLimitCombo.SelectedIndex];

        AppSettingsStore.Update(
            settings => settings with
            {
                CacheLimitBytes = limit
            });

        if (AutoCleanupToggle.IsOn)
            await CacheRuntime.EnforcePolicyAsync();

        await RefreshAsync();
    }

    private void PrecacheSizeCombo_SelectionChanged(
        object sender,
        SelectionChangedEventArgs e)
    {
        if (_isSynchronizing ||
            PrecacheSizeCombo.SelectedIndex < 0 ||
            PrecacheSizeCombo.SelectedIndex >=
            PrecacheSizes.Length)
        {
            return;
        }

        var size =
            PrecacheSizes[
                PrecacheSizeCombo.SelectedIndex];

        AppSettingsStore.Update(
            settings => settings with
            {
                RemotePrecacheBytes = size
            });
    }

    private void KeepOfflineToggle_Toggled(
        object sender,
        RoutedEventArgs e)
    {
        if (_isSynchronizing)
            return;

        AppSettingsStore.Update(
            settings => settings with
            {
                PreserveOfflineCache =
                    KeepOfflineToggle.IsOn
            });
    }

    private void SetBusy(
        bool busy)
    {
        _busy = busy;

        AutoCleanupToggle.IsEnabled = !busy;
        CacheLimitCombo.IsEnabled = !busy;
        PrecacheSizeCombo.IsEnabled = !busy;
        KeepOfflineToggle.IsEnabled = !busy;
        CacheList.IsEnabled = !busy;

        UpdateActionAvailability();
    }

    private void UpdateActionAvailability()
    {
        ClearApplicationCacheButton.IsEnabled =
            !_busy &&
            !_hasActiveDownloads;
    }

    private void ApplyText()
    {
        PageTitle.Text = T("Nav_Cache");
        PageSubtitle.Text = T("Cache_Subtitle");
        OverviewTitle.Text = T("Cache_Overview");

        ApplicationCacheTitle.Text =
            T("Cache_ApplicationCache");
        ApplicationCacheDescription.Text =
            T("Cache_ApplicationCacheDescription");
        ClearApplicationCacheButton.Content =
            T("Cache_ClearApplicationCache");

        PolicyTitle.Text = T("Cache_Policy");
        AutoCleanupTitle.Text = T("Cache_AutoCleanup");
        AutoCleanupDescription.Text =
            T("Cache_AutoCleanupDescription");
        CacheLimitTitle.Text = T("Cache_Limit");
        CacheLimitDescription.Text =
            T("Cache_LimitDescription");
        PrecacheTitle.Text = T("Cache_Precache");
        PrecacheDescription.Text =
            T("Cache_PrecacheDescription");
        KeepOfflineTitle.Text = T("Cache_KeepOffline");
        KeepOfflineDescription.Text =
            T("Cache_KeepOfflineDescription");

        VideoCacheTitle.Text =
            T("Cache_VideoCache");
        EmptyStateText.Text =
            T("Cache_VideoCacheEmpty");
    }

    private static int FindClosestIndex(
        IReadOnlyList<long> values,
        long requested)
    {
        var selected = 0;
        var delta =
            Math.Abs(values[0] - requested);

        for (var index = 1;
             index < values.Count;
             index++)
        {
            var candidate =
                Math.Abs(values[index] - requested);
            if (candidate >= delta)
                continue;

            delta = candidate;
            selected = index;
        }

        return selected;
    }

    private static string FormatBytes(
        long bytes)
    {
        var value = Math.Max(
            0,
            bytes);

        if (value < 1024)
        {
            return value.ToString(
                       CultureInfo.CurrentCulture) +
                   " B";
        }

        var units =
            new[] { "KB", "MB", "GB", "TB" };
        var size = (double)value;
        var unit = -1;

        do
        {
            size /= 1024d;
            unit++;
        }
        while (size >= 1024d &&
               unit < units.Length - 1);

        return size.ToString(
                   size >= 100
                       ? "0"
                       : size >= 10
                           ? "0.0"
                           : "0.00",
                   CultureInfo.CurrentCulture) +
               " " +
               units[unit];
    }
}
