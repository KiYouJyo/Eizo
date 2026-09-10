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

    private readonly MediaCredentialStore _credentials =
        MediaCredentialStore.Default;

    private readonly MediaScanCoordinator _scanCoordinator =
        MediaScanCoordinator.Default;

    private bool _isLoaded;

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
        AddSourceButton.Content = T("Source_Add");
    }

    private void SourcesView_Loaded(object sender, RoutedEventArgs e)
    {
        _isLoaded = true;

        _sources.Changed -= Sources_Changed;
        _sources.Changed += Sources_Changed;

        _catalog.Changed -= Catalog_Changed;
        _catalog.Changed += Catalog_Changed;

        _scanCoordinator.Changed -= ScanCoordinator_Changed;
        _scanCoordinator.Changed += ScanCoordinator_Changed;

        RefreshSources();
    }

    private void SourcesView_Unloaded(object sender, RoutedEventArgs e)
    {
        _isLoaded = false;
        _sources.Changed -= Sources_Changed;
        _catalog.Changed -= Catalog_Changed;
        _scanCoordinator.Changed -= ScanCoordinator_Changed;
    }

    private void Sources_Changed(object? sender, EventArgs e) =>
        DispatcherQueue.TryEnqueue(RefreshSources);

    private void Catalog_Changed(object? sender, EventArgs e) =>
        DispatcherQueue.TryEnqueue(RefreshSources);

    private void ScanCoordinator_Changed(object? sender, EventArgs e) =>
        DispatcherQueue.TryEnqueue(RefreshSources);

    private void RefreshSources()
    {
        if (SourceList is null)
            return;

        SourceList.ItemsSource = _sources
            .Snapshot()
            .Where(source => source.Enabled && !source.IsBuiltIn)
            .OrderBy(source => source.DisplayName, StringComparer.CurrentCultureIgnoreCase)
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
        var scan = _scanCoordinator.SnapshotForSource(source.Id);
        var isScanning = scan?.Status == MediaScanStatus.Running;

        var summaryParts = new List<string>();

        if (source.Kind == MediaSourceKind.WebDav)
        {
            summaryParts.Add(T("Source_WebDAV"));

            if (source.SelectedPaths is { Count: > 0 })
            {
                summaryParts.Add(
                    string.Format(
                        T("Sources_SelectedFoldersFormat"),
                        source.SelectedPaths.Count));
            }
            else
            {
                summaryParts.Add(
                    T("Sources_AllFolders"));
            }
        }

        if (isScanning && scan is not null)
        {
            var knownTotal = Math.Max(
                scan.DirectoriesProcessed,
                scan.KnownDirectoryTotal);
            var folderProgress = knownTotal > 0
                ? $"{scan.DirectoriesProcessed}/{knownTotal}"
                : scan.DirectoriesProcessed.ToString();

            summaryParts.Add(
                $"{T("Sources_Scanning")} {folderProgress}");
            summaryParts.Add(
                string.Format(
                    T("Sources_VideoCountFormat"),
                    scan.VideosDiscovered));
        }
        else
        {
            summaryParts.Add(
                string.Format(
                    T("Sources_VideoCountFormat"),
                    items.Count));
        }

        if (size > 0)
            summaryParts.Add(FormatBytes(size));

        if (!isScanning && source.LastScanUtc is { } lastScan)
        {
            summaryParts.Add(
                string.Format(
                    T("Sources_LastScanFormat"),
                    lastScan.ToLocalTime().ToString("g")));
        }

        var name = source.IsBuiltIn
            ? T("Sources_OpenedLocalFiles")
            : source.DisplayName;

        var kindLabel = source.Kind switch
        {
            MediaSourceKind.WebDav => T("Source_WebDAV"),
            MediaSourceKind.Local => T("Source_Local"),
            _ => source.Kind.ToString()
        };

        return new SourceItemModel(
            source.Id,
            name,
            string.Join(" · ", summaryParts),
            kindLabel,
            Removable: !source.IsBuiltIn && !isScanning,
            IsScanning: isScanning,
            CanScan: !isScanning && !source.IsBuiltIn,
            ScanText: isScanning
                ? T("Sources_Scanning")
                : T("Source_ScanNow"),
            ScanProgressSize: isScanning ? 16 : 0,
            ScanSpacing: isScanning ? 8 : 0);
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

        if (source.Kind == MediaSourceKind.WebDav && !item.IsScanning)
        {
            var editFolders = new MenuFlyoutItem
            {
                Text = T("Sources_EditReadFolders"),
                Icon = new FontIcon { Glyph = "\uE8B7" },
                Tag = source.Id
            };
            editFolders.Click += EditWebDavFoldersMenuItem_Click;
            flyout.Items.Add(editFolders);

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

    private async void SourceList_ItemClick(
        object sender,
        ItemClickEventArgs e)
    {
        if (e.ClickedItem is not SourceItemModel item || item.IsScanning)
            return;

        var source = _sources.Find(item.Id);
        if (source is not { Kind: MediaSourceKind.WebDav })
            return;

        await ShowWebDavEditorAsync(source);
    }

    private async void ScanSourceButton_Click(
        object sender,
        RoutedEventArgs e)
    {
        if (sender is not Button { Tag: string sourceId })
            return;

        await ScanSourceAsync(sourceId);
    }

    private async Task ScanSourceAsync(
        string sourceId)
    {
        var source = _sources.Find(sourceId);
        if (source is null || source.IsBuiltIn ||
            _scanCoordinator.IsScanning(sourceId))
        {
            return;
        }

        try
        {
            if (source.Kind == MediaSourceKind.Local &&
                !string.IsNullOrWhiteSpace(source.AccessToken) &&
                StorageApplicationPermissions.FutureAccessList.ContainsItem(
                    source.AccessToken))
            {
                _ = await StorageApplicationPermissions
                    .FutureAccessList
                    .GetFolderAsync(source.AccessToken);
            }
        }
        catch (Exception exception)
        {
            if (_isLoaded)
            {
                await ShowMessageAsync(
                    T("Sources_ScanFailed"),
                    exception.Message);
            }
            return;
        }

        var result = await _scanCoordinator.StartAsync(source);
        if (!_isLoaded || result.Status != MediaScanStatus.Failed)
            return;

        await ShowMessageAsync(
            T("Sources_ScanFailed"),
            FormatSourceError(
                result.ErrorCode,
                result.ErrorDetail));
    }

    private async void EditWebDavFoldersMenuItem_Click(
        object sender,
        RoutedEventArgs e)
    {
        if (sender is not MenuFlyoutItem { Tag: string sourceId } ||
            _scanCoordinator.IsScanning(sourceId))
        {
            return;
        }

        var source = _sources.Find(sourceId);
        if (source is not
            {
                Kind: MediaSourceKind.WebDav,
                RootLocation: { Length: > 0 }
            } ||
            !Uri.TryCreate(
                source.RootLocation,
                UriKind.Absolute,
                out var rootUri) ||
            !MediaSourceProviderRegistry.TryGet(
                MediaSourceKind.WebDav,
                out var provider) ||
            App.MainWindow is null)
        {
            return;
        }

        var connection =
            await provider.TestConnectionAsync(
                source);

        if (!connection.IsAvailable)
        {
            await ShowMessageAsync(
                T("Sources_ConnectionFailed"),
                FormatSourceError(
                    connection.ErrorCode,
                    connection.Detail));
            return;
        }

        var ownerHandle =
            WinRT.Interop.WindowNative.GetWindowHandle(
                App.MainWindow);

        var picker =
            new WebDavFolderPickerWindow(
                provider,
                source,
                rootUri,
                source.SelectedPaths,
                FormatSourceError);

        var selectedPaths =
            await picker.ShowAsync(
                ownerHandle);

        if (selectedPaths is null)
            return;

        _sources.AddWebDav(
            source.DisplayName,
            rootUri,
            source.UserName,
            source.CredentialKey,
            selectedPaths);

        await ShowMessageAsync(
            T("Sources_FoldersUpdated"),
            T("Sources_FoldersUpdatedNoScan"));
    }

    private void RemoveSourceMenuItem_Click(
        object sender,
        RoutedEventArgs e)
    {
        if (sender is not MenuFlyoutItem { Tag: string sourceId } ||
            _scanCoordinator.IsScanning(sourceId))
        {
            return;
        }

        var source = _sources.Find(sourceId);
        if (source is null || source.IsBuiltIn)
            return;

        _catalog.RemoveSourceItems(sourceId);
        _sources.Remove(sourceId);

        if (source.Kind == MediaSourceKind.WebDav)
            _credentials.RemoveWebDav(sourceId);

        if (!string.IsNullOrWhiteSpace(source.AccessToken) &&
            StorageApplicationPermissions.FutureAccessList.ContainsItem(
                source.AccessToken))
        {
            StorageApplicationPermissions.FutureAccessList.Remove(
                source.AccessToken);
        }
    }

    private void AddSourceButton_Click(
        object sender,
        RoutedEventArgs e)
    {
        var flyout = new MenuFlyout();

        var local = new MenuFlyoutItem
        {
            Text = T("Sources_AddLocalFolder"),
            Icon = new FontIcon { Glyph = "\uE8B7" }
        };
        local.Click += async (_, _) => await AddLocalSourceAsync();
        flyout.Items.Add(local);

        var webDav = new MenuFlyoutItem
        {
            Text = T("Sources_AddWebDav"),
            Icon = new FontIcon { Glyph = "\uE753" }
        };
        webDav.Click += async (_, _) => await AddWebDavSourceAsync();
        flyout.Items.Add(webDav);

        flyout.ShowAt(AddSourceButton);
    }

    private async Task AddLocalSourceAsync()
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
        }
        catch (Exception exception)
        {
            await ShowMessageAsync(
                T("Sources_AddLocalFailed"),
                exception.Message);
        }
    }

    private async Task AddWebDavSourceAsync() =>
        await ShowWebDavEditorAsync(existingSource: null);

    private async Task ShowWebDavEditorAsync(
        MediaSourceDefinition? existingSource)
    {
        var existingCredential = existingSource is null
            ? null
            : _credentials.GetWebDav(existingSource);

        var displayName = new TextBox
        {
            Header = T("Sources_DisplayName"),
            PlaceholderText = "NAS",
            Text = existingSource?.DisplayName ?? string.Empty
        };

        var address = new TextBox
        {
            Header = T("Sources_Address"),
            PlaceholderText = "https://example.com/dav/",
            Text = existingSource?.RootLocation ?? string.Empty
        };

        var userName = new TextBox
        {
            Header = T("Sources_Username"),
            Text = existingSource?.UserName ?? string.Empty
        };

        var password = new PasswordBox
        {
            Header = T("Sources_Password"),
            PlaceholderText = existingCredential is null
                ? string.Empty
                : T("Sources_PasswordKeepHint")
        };

        var panel = new StackPanel
        {
            Spacing = 12,
            MinWidth = 460
        };
        panel.Children.Add(displayName);
        panel.Children.Add(address);
        panel.Children.Add(userName);
        panel.Children.Add(password);

        if (XamlRoot is null)
            return;

        var dialogTheme = ResolveOverlayTheme();
        panel.RequestedTheme = dialogTheme;

        var dialog = new ContentDialog
        {
            XamlRoot = XamlRoot,
            RequestedTheme = dialogTheme,
            Title = existingSource is null
                ? T("Sources_WebDavDialogTitle")
                : T("Sources_EditWebDavTitle"),
            Content = panel,
            PrimaryButtonText = existingSource is null
                ? T("Sources_ConnectAndAdd")
                : T("Sources_SaveChanges"),
            CloseButtonText = T("Common_Cancel"),
            DefaultButton = ContentDialogButton.Primary
        };

        if (await dialog.ShowAsync() != ContentDialogResult.Primary)
            return;

        await CommitWebDavEditorAsync(
            existingSource,
            displayName.Text,
            address.Text,
            userName.Text,
            password.Password);
    }

    private async Task CommitWebDavEditorAsync(
        MediaSourceDefinition? existingSource,
        string displayName,
        string address,
        string userName,
        string password)
    {
        if (!TryNormalizeWebDavUri(
                address,
                out var normalizedRoot))
        {
            await ShowMessageAsync(
                T("Sources_ConnectionFailed"),
                T("Sources_InvalidAddress"));
            return;
        }

        var normalizedUser = userName.Trim();
        var name = string.IsNullOrWhiteSpace(displayName)
            ? normalizedRoot.Host
            : displayName.Trim();

        var preservedSelectedPaths =
            existingSource?.SelectedPaths?.ToList() ??
            [];

        var credential = ResolveEditorCredential(
            existingSource,
            normalizedUser,
            password);

        var sourceId = MediaSourceStore.BuildWebDavSourceId(
            normalizedRoot,
            normalizedUser);

        var collision = _sources.Find(sourceId);
        if (collision is not null &&
            !string.Equals(
                collision.Id,
                existingSource?.Id,
                StringComparison.Ordinal))
        {
            await ShowMessageAsync(
                T("Sources_ConnectionFailed"),
                T("Sources_SourceAlreadyExists"));
            return;
        }

        var temporarySource = new MediaSourceDefinition(
            sourceId,
            MediaSourceKind.WebDav,
            name,
            normalizedRoot.AbsoluteUri,
            UserName: normalizedUser,
            CredentialKey: credential is null
                ? null
                : "inline",
            SelectedPaths: preservedSelectedPaths);

        var temporaryProvider =
            new WebDavMediaSourceProvider(
                new InlineWebDavCredentialProvider(
                    credential));

        var test = await temporaryProvider.TestConnectionAsync(
            temporarySource);

        if (!test.IsAvailable)
        {
            await ShowMessageAsync(
                T("Sources_ConnectionFailed"),
                FormatSourceError(
                    test.ErrorCode,
                    test.Detail));
            return;
        }

        var previousTargetSource = _sources.Find(sourceId);
        var previousTargetCredential =
            previousTargetSource is null
                ? null
                : _credentials.GetWebDav(
                    previousTargetSource);

        try
        {
            var credentialKey = credential is null
                ? _credentials.SaveWebDav(
                    sourceId,
                    null,
                    null)
                : _credentials.SaveWebDav(
                    sourceId,
                    credential.UserName,
                    credential.Password);

            var savedSource = _sources.AddWebDav(
                name,
                normalizedRoot,
                normalizedUser,
                credentialKey,
                preservedSelectedPaths);

            if (existingSource is not null &&
                !string.Equals(
                    existingSource.Id,
                    savedSource.Id,
                    StringComparison.Ordinal))
            {
                _catalog.RemoveSourceItems(
                    existingSource.Id);
                _sources.Remove(
                    existingSource.Id);
                _credentials.RemoveWebDav(
                    existingSource.Id);
            }

            await ShowMessageAsync(
                T("Sources_ConnectionSucceeded"),
                string.Format(
                    existingSource is null
                        ? T("Sources_WebDavAddedNoScanFormat")
                        : T("Sources_WebDavUpdatedNoScanFormat"),
                    savedSource.DisplayName));
        }
        catch (Exception exception)
        {
            RollBackWebDavTarget(
                sourceId,
                previousTargetSource,
                previousTargetCredential);

            await ShowMessageAsync(
                T("Sources_ConnectionFailed"),
                exception is MediaSourceException sourceException
                    ? FormatSourceError(
                        sourceException.ErrorCode,
                        sourceException.Message)
                    : exception.Message);
        }
    }

    private void RollBackWebDavTarget(
        string sourceId,
        MediaSourceDefinition? previousSource,
        MediaCredentialSnapshot? previousCredential)
    {
        if (previousSource is null)
        {
            _catalog.RemoveSourceItems(sourceId);
            _sources.Remove(sourceId);
            _credentials.RemoveWebDav(sourceId);
            return;
        }

        if (!string.IsNullOrWhiteSpace(
                previousSource.RootLocation) &&
            Uri.TryCreate(
                previousSource.RootLocation,
                UriKind.Absolute,
                out var previousRoot))
        {
            _sources.AddWebDav(
                previousSource.DisplayName,
                previousRoot,
                previousSource.UserName,
                previousSource.CredentialKey,
                previousSource.SelectedPaths);
        }

        RestoreCredential(
            sourceId,
            previousCredential);
    }

    private MediaCredentialSnapshot? ResolveEditorCredential(
        MediaSourceDefinition? existingSource,
        string userName,
        string password)
    {
        if (!string.IsNullOrEmpty(password))
        {
            return new MediaCredentialSnapshot(
                userName,
                password);
        }

        if (string.IsNullOrEmpty(userName))
            return null;

        if (existingSource is not null)
        {
            var previous =
                _credentials.GetWebDav(
                    existingSource);

            if (previous is not null &&
                string.Equals(
                    previous.UserName,
                    userName,
                    StringComparison.Ordinal))
            {
                return previous;
            }
        }

        return new MediaCredentialSnapshot(
            userName,
            string.Empty);
    }

    private static bool TryNormalizeWebDavUri(
        string value,
        out Uri normalized)
    {
        normalized = null!;

        if (!Uri.TryCreate(
                value.Trim(),
                UriKind.Absolute,
                out var rootUri) ||
            (rootUri.Scheme != Uri.UriSchemeHttp &&
             rootUri.Scheme != Uri.UriSchemeHttps) ||
            !string.IsNullOrEmpty(rootUri.UserInfo))
        {
            return false;
        }

        normalized =
            NormalizeWebDavUri(rootUri);
        return true;
    }

    private void RestoreCredential(
        string sourceId,
        MediaCredentialSnapshot? previous)
    {
        if (previous is null)
        {
            _credentials.RemoveWebDav(sourceId);
            return;
        }

        _credentials.SaveWebDav(
            sourceId,
            previous.UserName,
            previous.Password);
    }

    private async Task ShowMessageAsync(string title, string message)
    {
        if (XamlRoot is null)
            return;

        await new ContentDialog
        {
            XamlRoot = XamlRoot,
            RequestedTheme = ResolveOverlayTheme(),
            Title = title,
            Content = message,
            CloseButtonText = T("Common_Close")
        }.ShowAsync();
    }

    private ElementTheme ResolveOverlayTheme()
    {
        if (ActualTheme is ElementTheme.Light or ElementTheme.Dark)
            return ActualTheme;

        var persisted = ThemePreferenceStore.Load();
        return persisted == ElementTheme.Default
            ? ElementTheme.Light
            : persisted;
    }

    private string FormatSourceError(
        string? errorCode,
        string? detail)
    {
        var code = errorCode switch
        {
            "AuthenticationFailed" => T("Sources_ErrorAuthentication"),
            "Forbidden" => T("Sources_ErrorForbidden"),
            "NotFound" => T("Sources_ErrorNotFound"),
            "Timeout" => T("Sources_ErrorTimeout"),
            "NetworkError" => T("Sources_ErrorNetwork"),
            "InvalidWebDavResponse" => T("Sources_ErrorInvalidResponse"),
            _ => T("Sources_ErrorGeneric")
        };

        return string.IsNullOrWhiteSpace(detail)
            ? code
            : $"{code}\n{detail}";
    }

    private static Uri NormalizeWebDavUri(Uri uri)
    {
        var builder = new UriBuilder(uri)
        {
            Fragment = string.Empty
        };

        if (!builder.Path.EndsWith("/", StringComparison.Ordinal))
            builder.Path += "/";

        return builder.Uri;
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

    private sealed class InlineWebDavCredentialProvider(
        MediaCredentialSnapshot? credential)
        : IMediaCredentialProvider
    {
        public MediaCredentialSnapshot? GetWebDav(
            MediaSourceDefinition source) =>
            credential;
    }
}
