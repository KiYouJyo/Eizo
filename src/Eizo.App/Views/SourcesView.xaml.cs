using Eizo.Localization;
using Eizo.Models;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Windows.Storage.AccessCache;
using Windows.Storage.Pickers;

namespace Eizo.Views;

public sealed partial class SourcesView : UserControl
{
    private readonly AppLocalizationService _localization =
        AppLocalizationService.Default;

    private readonly MediaSourceStore _sources =
        MediaSourceStore.Default;

    private readonly MediaCatalogStore _catalog =
        MediaCatalogStore.Default;

    public SourcesView()
    {
        InitializeComponent();
        ApplyText();

        Loaded += SourcesView_Loaded;
        Unloaded += SourcesView_Unloaded;

        RefreshSources();
    }

    private string T(string key) => _localization.GetString(key);

    private void ApplyText()
    {
        PageTitle.Text = T("Nav_Sources");
        PageSubtitle.Text = T("Sources_Subtitle");
        AddSourceButton.Content = T("Sources_AddLocalFolder");

        SectionList.ItemsSource =
            new[] { T("Sources_TabSources") };
    }

    private void SourcesView_Loaded(object sender, RoutedEventArgs e)
    {
        _sources.Changed -= Sources_Changed;
        _sources.Changed += Sources_Changed;

        _catalog.Changed -= Catalog_Changed;
        _catalog.Changed += Catalog_Changed;

        RefreshSources();
    }

    private void SourcesView_Unloaded(object sender, RoutedEventArgs e)
    {
        _sources.Changed -= Sources_Changed;
        _catalog.Changed -= Catalog_Changed;
    }

    private void Sources_Changed(object? sender, EventArgs e) =>
        DispatcherQueue.TryEnqueue(RefreshSources);

    private void Catalog_Changed(object? sender, EventArgs e) =>
        DispatcherQueue.TryEnqueue(RefreshSources);

    private void RefreshSources()
    {
        if (SourceList is null)
            return;

        SourceList.ItemsSource = _sources
            .Snapshot()
            .Where(source => source.Enabled)
            .OrderBy(source => source.IsBuiltIn ? 0 : 1)
            .ThenBy(source => source.DisplayName, StringComparer.CurrentCultureIgnoreCase)
            .Select(CreateSourceItem)
            .ToArray();
    }

    private SourceItemModel CreateSourceItem(MediaSourceDefinition source)
    {
        var items = _catalog.SnapshotForSource(source.Id);
        var size = items
            .Select(item => item.Location?.SizeBytes ?? 0L)
            .Where(value => value > 0)
            .Sum();

        var summaryParts = new List<string>
        {
            string.Format(
                T("Sources_VideoCountFormat"),
                items.Count)
        };

        if (size > 0)
            summaryParts.Add(FormatBytes(size));

        if (source.LastScanUtc is { } lastScan)
        {
            summaryParts.Add(
                string.Format(
                    T("Sources_LastScanFormat"),
                    lastScan.ToLocalTime().ToString("g")));
        }

        var name = source.IsBuiltIn
            ? T("Sources_OpenedLocalFiles")
            : source.DisplayName;

        return new SourceItemModel(
            source.Id,
            name,
            string.Join(" · ", summaryParts),
            source.Kind.ToString().ToLowerInvariant(),
            Removable: !source.IsBuiltIn);
    }

    private void SourceList_ContainerContentChanging(
        ListViewBase sender,
        ContainerContentChangingEventArgs args)
    {
        if (args.ItemContainer is not ListViewItem container ||
            args.Item is not SourceItemModel item)
        {
            return;
        }

        var source = _sources.Find(item.Id);
        if (source is null)
            return;

        var flyout = new MenuFlyout();

        if (source.Kind == MediaSourceKind.Local &&
            !string.IsNullOrWhiteSpace(source.RootLocation))
        {
            var scan = new MenuFlyoutItem
            {
                Text = T("Source_ScanNow"),
                Icon = new FontIcon { Glyph = "\uE72C" },
                Tag = source.Id
            };
            scan.Click += ScanSourceMenuItem_Click;
            flyout.Items.Add(scan);
        }

        if (item.Removable)
        {
            if (flyout.Items.Count > 0)
                flyout.Items.Add(new MenuFlyoutSeparator());

            var remove = new MenuFlyoutItem
            {
                Text = T("Common_Remove"),
                Icon = new FontIcon { Glyph = "\uE711" },
                Tag = source.Id
            };
            remove.Click += RemoveSourceMenuItem_Click;
            flyout.Items.Add(remove);
        }

        container.ContextFlyout =
            flyout.Items.Count > 0
                ? flyout
                : null;
        container.Tag = item;
    }

    private async void ScanSourceMenuItem_Click(
        object sender,
        RoutedEventArgs e)
    {
        if (sender is not MenuFlyoutItem { Tag: string sourceId })
            return;

        var source = _sources.Find(sourceId);
        if (source is null)
            return;

        try
        {
            if (!string.IsNullOrWhiteSpace(source.AccessToken) &&
                StorageApplicationPermissions.FutureAccessList.ContainsItem(
                    source.AccessToken))
            {
                _ = await StorageApplicationPermissions
                    .FutureAccessList
                    .GetFolderAsync(source.AccessToken);
            }

            await Task.Run(() => _catalog.ScanLocalSource(source));
        }
        catch
        {
            await ShowMessageAsync(
                T("Sources_ScanFailed"),
                source.DisplayName);
        }
    }

    private void RemoveSourceMenuItem_Click(
        object sender,
        RoutedEventArgs e)
    {
        if (sender is not MenuFlyoutItem { Tag: string sourceId })
            return;

        var source = _sources.Find(sourceId);
        if (source is null || source.IsBuiltIn)
            return;

        _catalog.RemoveSourceItems(sourceId);
        _sources.Remove(sourceId);

        if (!string.IsNullOrWhiteSpace(source.AccessToken) &&
            StorageApplicationPermissions.FutureAccessList.ContainsItem(
                source.AccessToken))
        {
            StorageApplicationPermissions.FutureAccessList.Remove(
                source.AccessToken);
        }
    }

    private async void AddSourceButton_Click(
        object sender,
        RoutedEventArgs e)
    {
        if (App.MainWindow is null)
            return;

        var picker = new FolderPicker
        {
            SuggestedStartLocation = PickerLocationId.VideosLibrary
        };
        picker.FileTypeFilter.Add("*");

        var hwnd = WinRT.Interop.WindowNative.GetWindowHandle(
            App.MainWindow);
        WinRT.Interop.InitializeWithWindow.Initialize(picker, hwnd);

        var folder = await picker.PickSingleFolderAsync();
        if (folder is null)
            return;

        try
        {
            var source = _sources.AddLocalFolder(
                folder.Path,
                folder.Name);

            StorageApplicationPermissions.FutureAccessList.AddOrReplace(
                source.Id,
                folder);

            source = _sources.AddLocalFolder(
                folder.Path,
                folder.Name,
                accessToken: source.Id);

            await Task.Run(() => _catalog.ScanLocalSource(source));
        }
        catch
        {
            await ShowMessageAsync(
                T("Sources_AddLocalFailed"),
                folder.Name);
        }
    }

    private async Task ShowMessageAsync(string title, string message)
    {
        if (XamlRoot is null)
            return;

        await new ContentDialog
        {
            XamlRoot = XamlRoot,
            Title = title,
            Content = message,
            CloseButtonText = T("Common_Close")
        }.ShowAsync();
    }

    private static string FormatBytes(long bytes)
    {
        string[] units = ["B", "KB", "MB", "GB", "TB"];
        var value = Math.Max(0d, bytes);
        var unit = 0;

        while (value >= 1024d && unit < units.Length - 1)
        {
            value /= 1024d;
            unit++;
        }

        return unit == 0
            ? $"{value:0} {units[unit]}"
            : $"{value:0.#} {units[unit]}";
    }
}
