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

    private bool _isSynchronizing;
    private bool _busy;

    public CacheView()
    {
        _isSynchronizing = true;
        InitializeComponent();

        CacheList.ItemsSource = _items;
        ApplyText();

        Loaded += CacheView_Loaded;
    }

    private string T(string key) =>
        _localization.GetString(key);

    private async void CacheView_Loaded(
        object sender,
        RoutedEventArgs e)
    {
        SynchronizePolicyControls();

        await CacheRuntime.RunStartupMaintenanceAsync();
        await RefreshAsync();
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
        _items.Clear();

        var videoEntries =
            snapshot.Entries
                .Where(static entry =>
                    entry.Category ==
                        CacheCategory.Media &&
                    entry.Pinned &&
                    !string.IsNullOrWhiteSpace(
                        entry.GroupKey))
                .ToArray();

        foreach (var group in videoEntries
                     .GroupBy(
                         static entry =>
                             entry.GroupKey!,
                         StringComparer.Ordinal))
        {
            var entries =
                group.ToArray();
            var newest =
                entries.Max(
                    static entry =>
                        entry.LastAccessedUtc);
            var first =
                entries[0];

            _items.Add(
                new CacheItemModel(
                    "group:" + group.Key,
                    first.DisplayName,
                    first.Source,
                    FormatBytes(
                        entries.Sum(
                            static entry =>
                                entry.SizeBytes)),
                    newest
                        .ToLocalTime()
                        .ToString(
                            "g",
                            CultureInfo.CurrentCulture)));
        }

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
    }

    private async void ClearApplicationCacheButton_Click(
        object sender,
        RoutedEventArgs e)
    {
        if (_busy)
            return;

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
            sender is not Button
            {
                Tag: string id
            })
        {
            return;
        }

        SetBusy(true);
        try
        {
            const string groupPrefix = "group:";

            if (id.StartsWith(
                    groupPrefix,
                    StringComparison.Ordinal))
            {
                await CacheRuntime.Store.ClearGroupAsync(
                    id[groupPrefix.Length..],
                    preservePinned: false);
            }
            else
            {
                await CacheRuntime.Store.DeleteAsync(id);
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

        ClearApplicationCacheButton.IsEnabled = !busy;
        AutoCleanupToggle.IsEnabled = !busy;
        CacheLimitCombo.IsEnabled = !busy;
        PrecacheSizeCombo.IsEnabled = !busy;
        KeepOfflineToggle.IsEnabled = !busy;
        CacheList.IsEnabled = !busy;
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
